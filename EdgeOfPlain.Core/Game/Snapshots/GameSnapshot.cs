using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Arch.Core;
using EdgeOfPlain.Core.Definitions.Worlds;
using EdgeOfPlain.Core.Game.PathFinding;
using FixedMathSharp;
using Tomlyn;

namespace EdgeOfPlain.Core.Game.Snapshots;

public sealed class GameSnapshot
{
	private const int FormatVersion = 1;
	private readonly int _primary;
	private readonly WorldState[] _worlds;

	private GameSnapshot(int primary, WorldState[] worlds)
	{
		_primary = primary;
		_worlds = worlds;
	}

	public static Task<byte[]> CaptureAsync(GameCore core, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(core);
		if (!core.IsPaused) throw new InvalidOperationException("Pause the game before capturing a snapshot.");
		cancellationToken.ThrowIfCancellationRequested();
		var worlds = core.Worlds.Select(CaptureWorld).ToArray();
		var snapshot = new GameSnapshot(core.PrimaryKey.Value, worlds);
		return Task.Run(() => snapshot.Encode(cancellationToken), cancellationToken);
	}

	public static Task<GameSnapshot> DecodeAsync(byte[] bytes, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(bytes);
		return Task.Run(() => Decode(bytes, cancellationToken), cancellationToken);
	}

	public void Apply(GameCore core)
	{
		ArgumentNullException.ThrowIfNull(core);
		if (!core.IsPaused) throw new InvalidOperationException("Pause the game before applying a snapshot.");
		var types = ComponentRegistry.TypeToComponentType.Keys.ToDictionary(ComponentSnapshotCodec.Signature);
		var prepared = new List<GameWorld>();
		try
		{
			foreach (var state in _worlds)
			{
				var world = new GameWorld(GameMap.Build(state.Map, state.ChunkSize), new WorldKey(state.Key));
				prepared.Add(world);
				var entities = new Entity[state.Entities.Length];
				var units = new Dictionary<UnitId, Entity>();
				for (var i = 0; i < entities.Length; i++)
				{
					var saved = state.Entities[i];
					var components = new object[saved.Components.Length];
					var signature = new ComponentType[components.Length];
					var seen = new HashSet<Type>();
					for (var j = 0; j < components.Length; j++)
					{
						var component = saved.Components[j];
						if (!types.TryGetValue(component.Type, out var type) || !seen.Add(type))
							throw new InvalidDataException($"Unknown or duplicate component '{component.Type}'.");
						components[j] = ComponentSnapshotCodec.For(type).Decode(component.Bytes);
						signature[j] = ComponentRegistry.TypeToComponentType[type];
					}
					var entity = world.World.Create(new Signature(signature));
					entities[i] = entity;
					foreach (var component in components) world.World.Set(entity, component);
					if (saved.UnitId != UnitId.None)
					{
						if (saved.UnitId < 1 || saved.UnitId > state.LastUnitId ||
						    !units.TryAdd(new UnitId(saved.UnitId), entity))
							throw new InvalidDataException("Invalid unit identity.");
					}
				}
				world.Units.Restore(units, state.LastUnitId);
				foreach (var path in state.Paths)
				{
					RequireEntity(path.Entity, entities.Length);
					if (path.Index < 0 || path.Index > path.Points.Length || path.Attempts < 0)
						throw new InvalidDataException("Invalid path state.");
					var handle = new EntityHandle(entities[path.Entity]);
					if (world.Pathfinding.HasPath(handle)) throw new InvalidDataException("Duplicate entity path.");
					world.Pathfinding.SetPath(handle, [.. path.Points], path.Target);
					var restored = world.Pathfinding.Paths[handle];
					restored.Index = path.Index;
					restored.RepathAttempts = path.Attempts;
				}
				var occupied = new Dictionary<EntityHandle, long[]>();
				foreach (var building in state.Occupancy)
				{
					RequireEntity(building.Entity, entities.Length);
					if (!occupied.TryAdd(new EntityHandle(entities[building.Entity]), building.Tiles))
						throw new InvalidDataException("Duplicate occupied entity.");
				}
				world.Pathfinding.Occupancy.Restore(occupied, state.OccupancyVersion);
			}
			core.ReplaceWorlds(prepared, new WorldKey(_primary));
			prepared.Clear();
		}
		finally
		{
			foreach (var world in prepared) world.Dispose();
		}
	}

	private static WorldState CaptureWorld(GameWorld world)
	{
		if (world.Commands.PendingCount != 0) throw new InvalidOperationException("Apply queued commands before synchronization.");
		var description = new QueryDescription();
		var entities = new Entity[world.World.CountEntities(in description)];
		world.World.GetEntities(in description, entities);
		var indices = new Dictionary<EntityHandle, int>();
		var saved = new EntityState[entities.Length];
		for (var i = 0; i < entities.Length; i++)
		{
			indices.Add(new EntityHandle(entities[i]), i);
			world.Units.TryGet(entities[i], out var id);
			var components = world.World.GetAllComponents(entities[i])
				.Select(component => component ?? throw new InvalidDataException("Null snapshot component."))
				.Select(component =>
				new ComponentState(ComponentSnapshotCodec.Signature(component.GetType()),
					ComponentSnapshotCodec.For(component.GetType()).Encode(component))).ToArray();
			saved[i] = new EntityState(id.Value, components);
		}
		var paths = world.Pathfinding.Paths.Select(pair =>
			new PathState(indices[pair.Key], pair.Value.TargetTile, pair.Value.Index,
				pair.Value.RepathAttempts, pair.Value.Waypoints.ToArray())).ToArray();
		var occupancy = world.Pathfinding.Occupancy.Buildings.Select(pair =>
			new OccupancyState(indices[pair.Key], pair.Value.ToArray())).ToArray();
		return new WorldState(world.Key.Value, world.Map.Export(), world.Map.ChunkSize, world.Units.LastIssuedId,
			world.Pathfinding.Occupancy.Version, saved, paths, occupancy);
	}

	private byte[] Encode(CancellationToken cancellationToken)
	{
		using var output = new MemoryStream();
		using (var compressed = new DeflateStream(output, CompressionLevel.Fastest, true))
		using (var writer = new BinaryWriter(compressed, Encoding.UTF8, true))
		{
			writer.Write(FormatVersion);
			writer.Write(_primary);
			writer.Write(_worlds.Length);
			foreach (var world in _worlds)
			{
				cancellationToken.ThrowIfCancellationRequested();
				writer.Write(world.Key);
				writer.Write(TomlSerializer.Serialize(world.Map));
				writer.Write(world.ChunkSize);
				writer.Write(world.LastUnitId);
				writer.Write(world.OccupancyVersion);
				writer.Write(world.Entities.Length);
				foreach (var entity in world.Entities)
				{
					cancellationToken.ThrowIfCancellationRequested();
					writer.Write(entity.UnitId);
					writer.Write(entity.Components.Length);
					foreach (var component in entity.Components)
					{
						writer.Write(component.Type);
						writer.Write(component.Bytes.Length);
						writer.Write(component.Bytes);
					}
				}
				writer.Write(world.Paths.Length);
				foreach (var path in world.Paths)
				{
					writer.Write(path.Entity);
					writer.Write(path.Target);
					writer.Write(path.Index);
					writer.Write(path.Attempts);
					writer.Write(path.Points.Length);
					foreach (var point in path.Points)
					{
						writer.Write(long.Parse(point.X.ToRawString(), System.Globalization.CultureInfo.InvariantCulture));
						writer.Write(long.Parse(point.Y.ToRawString(), System.Globalization.CultureInfo.InvariantCulture));
					}
				}
				writer.Write(world.Occupancy.Length);
				foreach (var building in world.Occupancy)
				{
					writer.Write(building.Entity);
					writer.Write(building.Tiles.Length);
					foreach (var tile in building.Tiles) writer.Write(tile);
				}
			}
		}
		return output.ToArray();
	}

	private static GameSnapshot Decode(byte[] bytes, CancellationToken cancellationToken)
	{
		using var input = new MemoryStream(bytes, false);
		using var deflate = new DeflateStream(input, CompressionMode.Decompress);
		using var expanded = new MemoryStream();
		var buffer = new byte[81920];
		int read;
		while ((read = deflate.Read(buffer)) != 0)
		{
			cancellationToken.ThrowIfCancellationRequested();
			expanded.Write(buffer, 0, read);
		}
		expanded.Position = 0;
		using var reader = new BinaryReader(expanded, Encoding.UTF8, true);
		if (reader.ReadInt32() != FormatVersion) throw new InvalidDataException("Unknown snapshot version.");
		var primary = reader.ReadInt32();
		var worlds = new WorldState[ReadCount(reader, sizeof(int))];
		var keys = new HashSet<int>();
		for (var i = 0; i < worlds.Length; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var key = reader.ReadInt32();
			if (key <= 0 || !keys.Add(key)) throw new InvalidDataException("Invalid world key.");
			var map = TomlSerializer.Deserialize<Map>(reader.ReadString());
			var chunkSize = reader.ReadInt32();
			var lastUnitId = reader.ReadInt32();
			var occupancyVersion = reader.ReadInt32();
			if (chunkSize <= 0 || lastUnitId < 0) throw new InvalidDataException("Invalid world state.");
			var entities = new EntityState[ReadCount(reader, 2 * sizeof(int))];
			for (var j = 0; j < entities.Length; j++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				var unitId = reader.ReadInt32();
				var components = new ComponentState[ReadCount(reader, sizeof(int) + sizeof(byte))];
				for (var k = 0; k < components.Length; k++)
				{
					var type = reader.ReadString();
					var data = reader.ReadBytes(ReadCount(reader, sizeof(byte)));
					components[k] = new ComponentState(type, data);
				}
				entities[j] = new EntityState(unitId, components);
			}
			var paths = new PathState[ReadCount(reader, 4 * sizeof(int) + sizeof(long))];
			for (var j = 0; j < paths.Length; j++)
			{
				var entity = reader.ReadInt32();
				var target = reader.ReadInt64();
				var index = reader.ReadInt32();
				var attempts = reader.ReadInt32();
				var points = new Vector2d[ReadCount(reader, 2 * sizeof(long))];
				for (var k = 0; k < points.Length; k++)
					points[k] = new Vector2d(Fixed64.FromRaw(reader.ReadInt64()), Fixed64.FromRaw(reader.ReadInt64()));
				paths[j] = new PathState(entity, target, index, attempts, points);
			}
			var buildings = new OccupancyState[ReadCount(reader, 2 * sizeof(int))];
			for (var j = 0; j < buildings.Length; j++)
			{
				var entity = reader.ReadInt32();
				var tiles = new long[ReadCount(reader, sizeof(long))];
				for (var k = 0; k < tiles.Length; k++) tiles[k] = reader.ReadInt64();
				buildings[j] = new OccupancyState(entity, tiles);
			}
			worlds[i] = new WorldState(key, map, chunkSize, lastUnitId, occupancyVersion, entities, paths, buildings);
		}
		if (expanded.Position != expanded.Length || (worlds.Length > 0 ? !keys.Contains(primary) : primary != 0))
			throw new InvalidDataException("Invalid snapshot contents.");
		return new GameSnapshot(primary, worlds);
	}

	private static int ReadCount(BinaryReader reader, int minimumBytes)
	{
		var count = reader.ReadInt32();
		if (count < 0 || count > (reader.BaseStream.Length - reader.BaseStream.Position) / minimumBytes)
			throw new InvalidDataException("Invalid snapshot length.");
		return count;
	}

	private static void RequireEntity(int index, int count)
	{
		if ((uint)index >= (uint)count) throw new InvalidDataException("Invalid entity reference.");
	}

	private sealed record ComponentState(string Type, byte[] Bytes);
	private sealed record EntityState(int UnitId, ComponentState[] Components);
	private sealed record PathState(int Entity, long Target, int Index, int Attempts, Vector2d[] Points);
	private sealed record OccupancyState(int Entity, long[] Tiles);
	private sealed record WorldState(int Key, Map Map, int ChunkSize, int LastUnitId, int OccupancyVersion,
		EntityState[] Entities, PathState[] Paths, OccupancyState[] Occupancy);
}
