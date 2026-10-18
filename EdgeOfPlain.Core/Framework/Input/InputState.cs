using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;

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

	private readonly Lock _gate = new();
	private readonly Queue<InputEvent> _pending = new();

	private Vector2 _position;
	private Vector2 _delta;
	private float _scroll;
	private bool _hasPointer;

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

		ApplyPending();
	}

	public void ApplyPending()
	{
		lock (_gate)
		{
			while (_pending.Count > 0) Apply(_pending.Dequeue());
		}
	}
	public void OnKeyDown(Key key) => Apply(new InputEvent(InputEventKind.KeyDown, key, default, 0f, 0f));

	public void OnKeyUp(Key key) => Apply(new InputEvent(InputEventKind.KeyUp, key, default, 0f, 0f));

	public void OnMouseButtonDown(MouseButton button) => Apply(new InputEvent(InputEventKind.MouseButtonDown, default, button, 0f, 0f));

	public void OnMouseButtonUp(MouseButton button) => Apply(new InputEvent(InputEventKind.MouseButtonUp, default, button, 0f, 0f));

	public void OnMouseMoved(float x, float y) => Apply(new InputEvent(InputEventKind.MouseMove, default, default, x, y));

	public void OnScroll(float delta) => Apply(new InputEvent(InputEventKind.Scroll, default, default, delta, 0f));

	public void Reset() => Apply(new InputEvent(InputEventKind.Reset, default, default, 0f, 0f));


	public void PostKeyDown(Key key) => Post(new InputEvent(InputEventKind.KeyDown, key, default, 0f, 0f));

	public void PostKeyUp(Key key) => Post(new InputEvent(InputEventKind.KeyUp, key, default, 0f, 0f));

	public void PostMouseButtonDown(MouseButton button) => Post(new InputEvent(InputEventKind.MouseButtonDown, default, button, 0f, 0f));

	public void PostMouseButtonUp(MouseButton button) => Post(new InputEvent(InputEventKind.MouseButtonUp, default, button, 0f, 0f));

	public void PostMouseMoved(float x, float y) => Post(new InputEvent(InputEventKind.MouseMove, default, default, x, y));

	public void PostScroll(float delta) => Post(new InputEvent(InputEventKind.Scroll, default, default, delta, 0f));

	public void PostReset() => Post(new InputEvent(InputEventKind.Reset, default, default, 0f, 0f));

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

	private void Post(in InputEvent inputEvent)
	{
		lock (_gate) _pending.Enqueue(inputEvent);
	}

	private void Apply(in InputEvent inputEvent)
	{
		switch (inputEvent.Kind)
		{
			case InputEventKind.KeyDown:
				SetKeyDown(inputEvent.Key);
				break;

			case InputEventKind.KeyUp:
				SetKeyUp(inputEvent.Key);
				break;

			case InputEventKind.MouseButtonDown:
				SetMouseButtonDown(inputEvent.Button);
				break;

			case InputEventKind.MouseButtonUp:
				SetMouseButtonUp(inputEvent.Button);
				break;

			case InputEventKind.MouseMove:
				SetMousePosition(inputEvent.X, inputEvent.Y);
				break;

			case InputEventKind.Scroll:
				_scroll += inputEvent.X;
				break;

			case InputEventKind.Reset:
				ClearHeld();
				break;
		}
	}

	private void SetKeyDown(Key key)
	{
		var index = (int)key;
		if ((uint)index >= (uint)_keyCount) return;

		if (_down[index]) return;

		_down[index] = true;
		_pressed[index] = true;
	}

	private void SetKeyUp(Key key)
	{
		var index = (int)key;
		if ((uint)index >= (uint)_keyCount) return;

		if (!_down[index]) return;

		_down[index] = false;
		_released[index] = true;
	}

	private void SetMouseButtonDown(MouseButton button)
	{
		var index = (int)button;
		if ((uint)index >= (uint)_buttonCount) return;

		if (_buttonDown[index]) return;

		_buttonDown[index] = true;
		_buttonPressed[index] = true;
	}

	private void SetMouseButtonUp(MouseButton button)
	{
		var index = (int)button;
		if ((uint)index >= (uint)_buttonCount) return;

		if (!_buttonDown[index]) return;

		_buttonDown[index] = false;
		_buttonReleased[index] = true;
	}

	private void SetMousePosition(float x, float y)
	{
		var next = new Vector2(x, y);

		if (_hasPointer) _delta += next - _position;

		_position = next;
		_hasPointer = true;
	}

	private void ClearHeld()
	{
		Array.Clear(_down);
		Array.Clear(_pressed);
		Array.Clear(_released);
		Array.Clear(_buttonDown);
		Array.Clear(_buttonPressed);
		Array.Clear(_buttonReleased);

		_delta = Vector2.Zero;
		_scroll = 0f;
		_hasPointer = false;
	}

	private enum InputEventKind
	{
		KeyDown,
		KeyUp,
		MouseButtonDown,
		MouseButtonUp,
		MouseMove,
		Scroll,
		Reset,
	}

	private readonly record struct InputEvent(InputEventKind Kind, Key Key, MouseButton Button, float X, float Y);
}
