namespace EdgeOfPlain.Core.Framework.Graphics;

public sealed class Camera2DState
{
	public Camera2DState(Camera2D current)
	{
		Previous = current;
		Current = current;
	}

	public Camera2D Previous { get; private set; }

	public Camera2D Current { get; private set; }

	public void Push(Camera2D next)
	{
		Previous = Current;
		Current = next;
	}

	public void Reset(Camera2D value)
	{
		Previous = value;
		Current = value;
	}

	public Camera2D Evaluate(float alpha) => Camera2D.Interpolate(Previous, Current, alpha);
}
