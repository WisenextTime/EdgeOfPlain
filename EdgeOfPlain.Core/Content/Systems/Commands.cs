using System;
using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Framework.Mods;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Game.PathFinding;
using EdgeOfPlain.Core.Game.Waypoint;
using EdgeOfPlain.Core.Utils;
using FixedMathSharp;
using LiteNetLib.Utils;

namespace EdgeOfPlain.Core.Content.Systems;

public record struct SpawnEntityCommand(EntityDef Definition, TransformComponent Transform, int Team)
	: ICommand, INetSerializable, IWorldScopedCommand
{
	public WorldKey World;

	readonly WorldKey IWorldScopedCommand.WorldKey => World;

	public void Serialize(NetDataWriter writer)
	{
		writer.Put(World.Value);
		writer.Put(Definition.FullId);
		NetSerilizeUtils.PutFixed(writer, Transform.Position.X);
		NetSerilizeUtils.PutFixed(writer, Transform.Position.Y);
		NetSerilizeUtils.PutFixed(writer, Transform.Rotation);
		writer.Put(Team);
	}

	public void Deserialize(NetDataReader reader)
	{
		var world = new WorldKey(reader.GetInt());
		var definition = ContentStore.Resolve<EntityDef>(reader.GetString());
		var x = NetSerilizeUtils.ReadFixed(reader);
		var y = NetSerilizeUtils.ReadFixed(reader);
		var rotation = NetSerilizeUtils.ReadFixed(reader);
		var position = new Vector2d(x, y);
		this = new SpawnEntityCommand(definition, new TransformComponent
		{
			Position = position,
			PreviousPosition = position,
			Rotation = rotation,
			PreviousRotation = rotation,
		}, reader.GetInt())
		{
			World = world,
		};
	}
}

public record struct DeleteEntityCommand(UnitId Target) : ICommand, INetSerializable, IWorldScopedCommand
{
	public WorldKey World;

	readonly WorldKey IWorldScopedCommand.WorldKey => World;

	public void Serialize(NetDataWriter writer)
	{
		writer.Put(World.Value);
		Target.Serialize(writer);
	}

	public void Deserialize(NetDataReader reader)
	{
		var world = new WorldKey(reader.GetInt());
		UnitId target = default;
		target.Deserialize(reader);
		World = world;
		Target = target;
	}
}

public record struct WaypointCommand(
	UnitGroup Units,
	WaypointType Type,
	WaypointAddType AddType,
	IWaypointTarget Target)
	: ICommand, INetSerializable, IWorldScopedCommand
{
	public WorldKey World;

	readonly WorldKey IWorldScopedCommand.WorldKey => World;

	private const byte TargetGround = 0;
	private const byte TargetUnit = 1;

	public void Serialize(NetDataWriter writer)
	{
		writer.Put(World.Value);
		writer.Put((ushort)Units.Count);
		foreach (var unit in Units.Units) unit.Serialize(writer);
		writer.PutEnum(Type);
		writer.PutEnum(AddType);

		switch (Target)
		{
			case GroundTarget ground:
				writer.Put(TargetGround);
				NetSerilizeUtils.PutFixed(writer, ground.Position.X);
				NetSerilizeUtils.PutFixed(writer, ground.Position.Y);
				break;
			case UnitTarget unit:
				writer.Put(TargetUnit);
				unit.Unit.Serialize(writer);
				break;
			default:
				throw new InvalidOperationException(
					$"Waypoint target '{Target?.GetType().FullName ?? "null"}' cannot be serialized.");
		}
	}

	public void Deserialize(NetDataReader reader)
	{
		World = new WorldKey(reader.GetInt());

		var count = reader.GetUShort();
		var units = new UnitId[count];
		for (var i = 0; i < count; i++)
		{
			units[i] = default;
			units[i].Deserialize(reader);
		}

		Units = new UnitGroup(units);
		Type = reader.GetEnum<WaypointType>();
		AddType = reader.GetEnum<WaypointAddType>();

		Target = reader.GetByte() switch
		{
			TargetGround => new GroundTarget(new Vector2d(
				NetSerilizeUtils.ReadFixed(reader),
				NetSerilizeUtils.ReadFixed(reader))),
			TargetUnit => ReadUnitTarget(reader),
			var unknown => throw new InvalidOperationException($"Unknown waypoint target tag {unknown}."),
		};
	}

	private static UnitTarget ReadUnitTarget(NetDataReader reader)
	{
		UnitId id = default;
		id.Deserialize(reader);
		return new UnitTarget(id);
	}
}