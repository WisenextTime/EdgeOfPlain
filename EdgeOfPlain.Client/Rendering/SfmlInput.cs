using System;
using System.Collections.Generic;
using System.Linq;
using EdgeOfPlain.Core.Framework.Input;
using SFML.Window;

namespace EdgeOfPlain.Client.Rendering;

public static class SfmlInput
{
	private static readonly Key[] _keys = BuildKeys();
	private static readonly MouseButton[] _buttons = BuildButtons();

	public static Key Map(Keyboard.Key key)
	{
		var index = (int)key;
		return (uint)index < (uint)_keys.Length ? _keys[index] : Key.Unknown;
	}

	public static MouseButton Map(Mouse.Button button)
	{
		var index = (int)button;
		return (uint)index < (uint)_buttons.Length ? _buttons[index] : MouseButton.Left;
	}

	public static void Attach(GameWindow window, InputState input)
	{
		ArgumentNullException.ThrowIfNull(window);
		ArgumentNullException.ThrowIfNull(input);

		window.KeyPressed += code => input.PostKeyDown(Map(code));
		window.KeyReleased += code => input.PostKeyUp(Map(code));
		window.MouseButtonPressed += button => input.PostMouseButtonDown(Map(button));
		window.MouseButtonReleased += button => input.PostMouseButtonUp(Map(button));
		window.MouseMoved += input.PostMouseMoved;
		window.MouseWheelScrolled += input.PostScroll;

		window.FocusLost += input.PostReset;
	}

	private static Key[] BuildKeys()
	{
		var values = Enum.GetValues<Keyboard.Key>();
		var table = new Key[TableSize(values.Select(value => (int)value))];
		Array.Fill(table, Key.Unknown);

		foreach (var value in values)
		{
			var index = (int)value;
			if (index < 0) continue;

			table[index] = Enum.TryParse<Key>(value.ToString(), out var key) ? key : Key.Unknown;
		}

		return table;
	}

	private static MouseButton[] BuildButtons()
	{
		var values = Enum.GetValues<Mouse.Button>();
		var table = new MouseButton[TableSize(values.Select(value => (int)value))];
		Array.Fill(table, MouseButton.Left);

		foreach (var value in values)
		{
			var index = (int)value;
			if (index < 0) continue;

			table[index] = Enum.TryParse<MouseButton>(value.ToString(), out var button) ? button : MouseButton.Left;
		}

		return table;
	}

	private static int TableSize(IEnumerable<int> values) => values.Select(value => value + 1).Prepend(0).Max();

}