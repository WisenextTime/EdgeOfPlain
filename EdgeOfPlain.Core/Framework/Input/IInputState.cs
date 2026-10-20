using System.Numerics;

namespace EdgeOfPlain.Core.Framework.Input;

public interface IInputState
{
	bool IsKeyDown(Key key);
	bool WasKeyPressed(Key key);
	bool WasKeyReleased(Key key);

	bool IsMouseButtonDown(MouseButton button);

	bool WasMouseButtonPressed(MouseButton button);

	bool WasMouseButtonReleased(MouseButton button);

	Vector2 MousePosition { get; }
	Vector2 MouseDelta { get; }
	float ScrollDelta { get; }
}