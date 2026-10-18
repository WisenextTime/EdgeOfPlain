using System;
using System.Drawing;
using System.Numerics;

namespace EdgeOfPlain.Core.Framework.Graphics;

public readonly record struct Camera2D(Vector2 Position, float Zoom = 1f, float Rotation = 0f)
{
	public const float MinZoom = 0.0001f;

	public static Camera2D Default => new(Vector2.Zero);

	public Vector2 WorldSize(Vector2 targetPixels)
	{
		RequireZoom();
		return targetPixels / Zoom;
	}

	public RectangleF WorldBounds(Vector2 targetPixels)
	{
		var size = WorldSize(targetPixels);
		return new RectangleF(Position.X - size.X / 2f, Position.Y - size.Y / 2f, size.X, size.Y);
	}

	public Vector2 WorldToScreen(Vector2 world, Vector2 targetPixels)
	{
		RequireZoom();
		return targetPixels / 2f + Rotate((world - Position) * Zoom, -Rotation);
	}

	public Vector2 ScreenToWorld(Vector2 screenPixels, Vector2 targetPixels)
	{
		RequireZoom();
		return Position + Rotate(screenPixels - targetPixels / 2f, Rotation) / Zoom;
	}

	public Camera2D ClampTo(RectangleF worldBounds, Vector2 targetPixels)
	{
		var half = WorldSize(targetPixels) / 2f;

		var x = worldBounds.Width <= half.X * 2f
			? worldBounds.X + worldBounds.Width / 2f
			: Math.Clamp(Position.X, worldBounds.Left + half.X, worldBounds.Right - half.X);

		var y = worldBounds.Height <= half.Y * 2f
			? worldBounds.Y + worldBounds.Height / 2f
			: Math.Clamp(Position.Y, worldBounds.Top + half.Y, worldBounds.Bottom - half.Y);

		return this with { Position = new Vector2(x, y) };
	}

	public static Camera2D Interpolate(in Camera2D from, in Camera2D to, float alpha)
	{
		var position = Vector2.Lerp(from.Position, to.Position, alpha);
		var zoom = from.Zoom + (to.Zoom - from.Zoom) * alpha;
		var rotation = from.Rotation + ShortestDelta(from.Rotation, to.Rotation) * alpha;

		return new Camera2D(position, zoom, rotation);
	}

	public static float NormalizeAngle(float radians)
	{
		var wrapped = MathF.IEEERemainder(radians, MathF.Tau);
		return wrapped <= -MathF.PI ? wrapped + MathF.Tau : wrapped;
	}

	private static float ShortestDelta(float from, float to) => NormalizeAngle(to - from);

	private static Vector2 Rotate(Vector2 value, float radians)
	{
		var cos = MathF.Cos(radians);
		var sin = MathF.Sin(radians);

		return new Vector2(value.X * cos - value.Y * sin, value.X * sin + value.Y * cos);
	}

	private void RequireZoom()
	{
		if (Zoom is < MinZoom || !float.IsFinite(Zoom))
			throw new ArgumentOutOfRangeException(nameof(Zoom), Zoom, "Camera zoom must be a positive, finite number.");
	}
}
