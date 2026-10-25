using Arch.Core;

namespace EdgeOfPlain.Core.Game;

public class GameWorld(GameMap map)
{
	private GameMap _map = map;
	private World _world = World.Create();

}