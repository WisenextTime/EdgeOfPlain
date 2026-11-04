using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Game.PathFinding;
using EdgeOfPlain.Core.Game.Waypoint;
using LiteNetLib.Utils;

namespace EdgeOfPlain.Core.Content.Systems;

public record struct SpawnEntityCommand(EntityDef Definition, TransformComponent Transform, int Team) : ICommand
{

	public void Serialize(NetDataWriter writer)
	{

	}

	public void Deserialize(NetDataReader reader)
	{

	}
}

public record struct DeleteEntityCommand(EntityHandle Target) : ICommand
{

	public void Serialize(NetDataWriter writer)
	{

	}

	public void Deserialize(NetDataReader reader)
	{

	}
}

public record struct WaypointCommand(
	UnitGroup Units,
	WaypointType Type,
	WaypointAddType AddType,
	IWaypointTarget Target)
	: ICommand
{

	public void Serialize(NetDataWriter writer)
	{

	}

	public void Deserialize(NetDataReader reader)
	{

	}
}