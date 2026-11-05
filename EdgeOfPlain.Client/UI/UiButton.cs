using System;
using System.Numerics;
using EdgeOfPlain.Core.Framework.Graphics;

namespace EdgeOfPlain.Client.UI;

public sealed class UiButton : UiElement
{

	public string Text { get; set; } = string.Empty;

	public int TextSize { get; set; } = 18;

	public ColorF Fill { get; set; } = new(58, 64, 76, 235);

	public ColorF HoverFill { get; set; } = new(80, 88, 104, 245);

	public ColorF PressedFill { get; set; } = new(44, 49, 58, 255);

	public ColorF Border { get; set; } = new(118, 128, 148, 255);

	public float BorderThickness { get; set; } = 1f;

	public string? BackgroundTexture { get; set; }

	public NineSlice? Background { get; set; }

	public event Action<UiButton>? Clicked;

	public bool IsPressed { get; private set; }

	protected override bool CapturesPointer => IsPressed;

	protected override bool OnPointer(in PointerInput pointer)
	{
		if (pointer.Pressed)
		{
			IsPressed = true;

			return true;
		}

		if (pointer.Released && IsPressed)
		{
			IsPressed = false;

			if (IsHovered) Clicked?.Invoke(this);

			return true;
		}

		return IsHovered || IsPressed;
	}

	protected override void OnDraw(ISpriteBatch batch)
	{
		var fill = IsPressed && IsHovered ? PressedFill : IsHovered ? HoverFill : Fill;

		DrawBackground(batch, fill);

		if (string.IsNullOrEmpty(Text)) return;

		var text = batch.MeasureText(Text, TextSize);

		batch.DrawText(
			Text,
			TextSize,
			Bounds.X + (Bounds.Width - text.X) / 2f,
			Bounds.Y + (Bounds.Height - text.Y) / 2f,
			(uint)(Layer + 1));
	}

	private void DrawBackground(ISpriteBatch batch, ColorF fill)
	{
		if (Background is { } slice && !string.IsNullOrEmpty(BackgroundTexture))
		{
			batch.DrawNineSlice(BackgroundTexture, slice, Bounds, (uint)Layer, fill);

			return;
		}

		var quad = new RenderMetadata
		{
			Layer = (uint)Layer,
			Color = fill,
			Size = Size,
		};

		batch.Draw(quad, Bounds.X, Bounds.Y);

		if (BorderThickness <= 0f || Border.A == 0) return;

		var thickness = BorderThickness;
		var border = new RenderMetadata
		{
			Layer = (uint)Layer,
			Color = Border,
		};

		DrawBar(batch, border, Bounds.X, Bounds.Y, Bounds.Width, thickness);
		DrawBar(batch, border, Bounds.X, Bounds.Bottom - thickness, Bounds.Width, thickness);
		DrawBar(batch, border, Bounds.X, Bounds.Y + thickness, thickness, Bounds.Height - thickness * 2f);
		DrawBar(batch, border, Bounds.Right - thickness, Bounds.Y + thickness, thickness, Bounds.Height - thickness * 2f);
	}

	private static void DrawBar(ISpriteBatch batch, RenderMetadata metadata, float x, float y, float width, float height)
	{
		if (width <= 0f || height <= 0f) return;

		metadata.Size = new Vector2(width, height);

		batch.Draw(metadata, x, y);
	}
}