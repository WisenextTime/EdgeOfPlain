using System;
using System.Drawing;
using System.Numerics;
using EdgeOfPlain.Core.Framework.Graphics;
using EdgeOfPlain.Core.Framework.Input;

namespace EdgeOfPlain.Client.UI;

public sealed class UiRoot : UiElement
{
	public UiRoot()
	{
		AnchorMin = Vector2.Zero;
		AnchorMax = Vector2.One;
	}

	public Vector2 Viewport => Size;

	public bool PointerOverUi { get; private set; }

	public void Resize(Vector2 viewport)
	{
		if (viewport.X <= 0f || viewport.Y <= 0f) return;

		Layout(viewport);
	}

	public bool Update(IInputState input)
	{
		ArgumentNullException.ThrowIfNull(input);

		return UpdatePointer(new PointerInput(
			input.MousePosition,
			input.IsMouseButtonDown(MouseButton.Left),
			input.WasMouseButtonPressed(MouseButton.Left),
			input.WasMouseButtonReleased(MouseButton.Left)));
	}

	public bool UpdatePointer(in PointerInput pointer)
	{
		PointerOverUi = base.Update(pointer);

		return PointerOverUi;
	}
}
