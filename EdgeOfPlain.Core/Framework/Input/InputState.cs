using System;
using System.Linq;
using System.Numerics;

namespace EdgeOfPlain.Core.Framework.Input;

public sealed class InputState : IInputState
{
	private static readonly int _keyCount = (int)Enum.GetValues<Key>().Max() + 1;
	private static readonly int _buttonCount = (int)Enum.GetValues<MouseButton>().Max() + 1;

	private readonly bool[] _down = new bool[_keyCount];
	private readonly bool[] _pressed = new bool[_keyCount];
	private readonly bool[] _released = new bool[_keyCount];

	private readonly bool[] _buttonDown = new bool[_buttonCount];
	private readonly bool[] _buttonPressed = new bool[_buttonCount];
	private readonly bool[] _buttonReleased = new bool[_buttonCount];

	private Vector2 _position;
	private Vector2 _delta;
	private float _scroll;

	public Vector2 MousePosition => _position;

	public Vector2 MouseDelta => _delta;

	public float ScrollDelta => _scroll;
	public void BeginFrame()
	{
		Array.Clear(_pressed);
		Array.Clear(_released);
		Array.Clear(_buttonPressed);
		Array.Clear(_buttonReleased);

		_delta = Vector2.Zero;
		_scroll = 0f;
	}

	public void OnKeyDown(Key key)
	{
		var index = (int)key;
		if ((uint)index >= (uint)_keyCount) return;

		if (_down[index]) return;

		_down[index] = true;
		_pressed[index] = true;
	}

	public void OnKeyUp(Key key)
	{
		var index = (int)key;
		if ((uint)index >= (uint)_keyCount) return;

		if (!_down[index]) return;

		_down[index] = false;
		_released[index] = true;
	}

	public void OnMouseButtonDown(MouseButton button)
	{
		var index = (int)button;
		if ((uint)index >= (uint)_buttonCount) return;

		if (_buttonDown[index]) return;

		_buttonDown[index] = true;
		_buttonPressed[index] = true;
	}

	public void OnMouseButtonUp(MouseButton button)
	{
		var index = (int)button;
		if ((uint)index >= (uint)_buttonCount) return;

		if (!_buttonDown[index]) return;

		_buttonDown[index] = false;
		_buttonReleased[index] = true;
	}

	public void OnMouseMoved(float x, float y)
	{
		var next = new Vector2(x, y);
		_delta += next - _position;
		_position = next;
	}

	public void OnScroll(float delta) => _scroll += delta;

	public void Reset()
	{
		Array.Clear(_down);
		Array.Clear(_pressed);
		Array.Clear(_released);
		Array.Clear(_buttonDown);
		Array.Clear(_buttonPressed);
		Array.Clear(_buttonReleased);

		_delta = Vector2.Zero;
		_scroll = 0f;
	}

	public bool IsKeyDown(Key key)
	{
		var index = (int)key;
		return (uint)index < (uint)_keyCount && _down[index];
	}

	public bool WasKeyPressed(Key key)
	{
		var index = (int)key;
		return (uint)index < (uint)_keyCount && _pressed[index];
	}

	public bool WasKeyReleased(Key key)
	{
		var index = (int)key;
		return (uint)index < (uint)_keyCount && _released[index];
	}

	public bool IsMouseButtonDown(MouseButton button)
	{
		var index = (int)button;
		return (uint)index < (uint)_buttonCount && _buttonDown[index];
	}

	public bool WasMouseButtonPressed(MouseButton button)
	{
		var index = (int)button;
		return (uint)index < (uint)_buttonCount && _buttonPressed[index];
	}

	public bool WasMouseButtonReleased(MouseButton button)
	{
		var index = (int)button;
		return (uint)index < (uint)_buttonCount && _buttonReleased[index];
	}
}
