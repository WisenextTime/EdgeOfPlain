using System;
using Arch.Core;

namespace EdgeOfPlain.Core.Game;

public class GameWorld(GameMap map) : IDisposable
{
	private GameMap _map = map;
	private readonly World _world = World.Create();

	public void Dispose()
	{
		_world.Dispose();
		GC.SuppressFinalize(this);
	}
}