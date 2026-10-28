using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Game.Systems;
using EdgeOfPlain.Core.Game.Waypoint;

namespace EdgeOfPlain.Core.Content.Systems;

public record struct UnitDiedCommand(EntityHandle Entity) : ICommand;
public record struct SpawnUnitCommand(UnitDef Definition, TransformComponent Transform) : ICommand;
public record struct DeleteUnitCommand(EntityHandle Target) : ICommand;

public record struct WaypointCommand(
	EntityHandle Entity,
	WaypointType Type,
	WaypointAddType AddType,
	IWaypointTarget Target) 
	: ICommand;
