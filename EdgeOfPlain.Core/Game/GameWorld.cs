using System;
using Arch.Core;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Game.PathFinding;
using FixedMathSharp;

namespace EdgeOfPlain.Core.Game
{
	public class GameWorld : IDisposable
	{
		private bool _ownsWorld = true;

		public GameWorld(GameMap map) : this(map, WorldKey.Primary)
		{
		}

		public GameWorld(GameMap map, WorldKey key)
		{
			Map = map ?? throw new ArgumentNullException(nameof(map));
			if (!key.IsValid) throw new ArgumentOutOfRangeException(nameof(key), key, "World key must be positive.");

			Key = key;
			WorldPathfinding.Create(World, Map);
			Units = UnitRegistry.Create(World);
			Dispatcher = new CommandDispatcher(Commands);
		}

		public WorldKey Key { get; }

		public GameMap Map { get; private set; }
		public bool IsPaused { get; internal set; }

		public World World { get; private set; } = World.Create();

		public UnitRegistry Units { get; private set; }

		public CommandBus Commands { get; } = new();

		public CommandDispatcher Dispatcher { get; }

		public WorldPathfinding Pathfinding => WorldPathfinding.TryGet(World)
		                                       ?? throw new InvalidOperationException(
			                                       "World pathfinding is not registered for this world.");

		public int RunFrameSystems(Fixed64 delta)
		{
			if (IsPaused) return 0;
			var systems = CommandRegistry.FrameSystems;
			if (systems.Count == 0) return 0;

			foreach (var system in systems) system.Operation(World, delta);

			return systems.Count;
		}

		public int ApplyCommands() => IsPaused ? 0 : Dispatcher.ApplyAll(World);

		public UnitId IdOf(EntityHandle handle) => Units.Register(handle.Value);

		public bool TryGetId(EntityHandle handle, out UnitId id) => Units.TryGet(handle.Value, out id);

		public bool TryResolve(UnitId id, out EntityHandle handle)
		{
			if (Units.TryResolve(World, id, out var entity))
			{
				handle = new EntityHandle(entity);
				return true;
			}

			handle = default;
			return false;
		}

		public UnitGroup Select(params EntityHandle[] handles)
		{
			var ids = new UnitId[handles.Length];
			for (var i = 0; i < handles.Length; i++) ids[i] = Units.Register(handles[i].Value);
			return new UnitGroup(ids);
		}

		public int CollectEntities(in QueryDescription description, Span<EntityHandle> destination)
		{
			var count = World.CountEntities(in description);
			if (count == 0) return 0;

			var entities = count <= 256 ? stackalloc Entity[count] : new Entity[count];
			World.GetEntities(in description, entities);

			var copy = Math.Min(count, destination.Length);
			for (var i = 0; i < copy; i++)
			{
				destination[i] = new EntityHandle(entities[i]);
			}

			return count;
		}

		public int Step(Fixed64 delta)
		{
			RunFrameSystems(delta);
			return ApplyCommands();
		}

		public int Step() => Step(Fixed64.Zero);

		internal void Adopt(GameWorld state)
		{
			Dispose();
			World = state.World;
			Units = state.Units;
			Map = state.Map;
			Commands.Clear();
			_ownsWorld = true;
			state._ownsWorld = false;
		}

		public void Dispose()
		{
			if (!_ownsWorld) return;
			_ownsWorld = false;
			WorldPathfinding.Dispose(World);
			UnitRegistry.Dispose(World);
			World.Dispose();
			GC.SuppressFinalize(this);
		}
	}
}
