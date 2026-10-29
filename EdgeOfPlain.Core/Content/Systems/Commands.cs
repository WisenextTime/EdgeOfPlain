using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Game.PathFinding;
using EdgeOfPlain.Core.Game.Systems;
using EdgeOfPlain.Core.Game.Waypoint;

namespace EdgeOfPlain.Core.Content.Systems;

public record struct SpawnEntityCommand(EntityDef Definition, TransformComponent Transform, int Team) : ICommand;
public record struct DeleteEntityCommand(EntityHandle Target) : ICommand;

public record struct WaypointCommand(
	UnitGroup Units,
	WaypointType Type,
	WaypointAddType AddType,
	IWaypointTarget Target) 
	: ICommand;
