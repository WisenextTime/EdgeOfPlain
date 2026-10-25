using System;

namespace EdgeOfPlain.Core.Framework.Graphics;

public sealed class Camera2DState(Camera2D current)
{
	public Camera2D Previous { get; private set; } = current;

	public Camera2D Current { get; private set; } = current;

	public void Push(Camera2D next)
	{
		if (next == Current)
		{
			Previous = Current;

			return;
		}

		Previous = Current;
		Current = next;
	}
	public void Set(Camera2D value)
	{
		Previous = value;
		Current = value;
	}

	public void Reset(Camera2D value) => Set(value);

	public Camera2D Evaluate(float alpha) => Camera2D.Interpolate(Previous, Current, Math.Clamp(alpha, 0f, 1f));
}
