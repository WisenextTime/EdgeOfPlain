using System.Numerics;

namespace EdgeOfPlain.Scr.Core.Lib;

public static class ExactMath
{
	public static Godot.Vector2 GetAbstractPosition(Godot.Vector2 source, Godot.Vector2 target)
	{
		return new Godot.Vector2(target.X - source.X, target.Y - source.Y);
	}
}