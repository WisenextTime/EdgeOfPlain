using System.Drawing;
using System.Numerics;

namespace EdgeOfPlain.Core.Framework.Graphics;

public sealed record RenderMetadata
{
	public string? Texture { get; set; }

	public string? Shader { get; set; }

	public uint Layer { get; set; }

	public ColorF Color { get; set; } = ColorF.White;

	public Rectangle? Source { get; set; }

	public Vector2? Size { get; set; }

	public Vector2 Scale { get; set; } = Vector2.One;

	/// <summary>Radians, clockwise on screen.</summary>
	public float Rotation { get; set; }

	/// <summary>Pivot in source pixels, relative to <see cref="Source"/> or the whole texture. Not affected by <see cref="Size"/> or <see cref="Scale"/>.</summary>
	public Vector2 Origin { get; set; }
}
