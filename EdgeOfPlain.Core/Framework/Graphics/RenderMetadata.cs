using System.Drawing;
using System.Numerics;
using Zio;

namespace EdgeOfPlain.Core.Framework.Graphics
{
	public sealed record RenderMetadata
	{
		public UPath? Texture { get; set; }
		public UPath? Shader { get; set; }
		public uint Layer { get; set; }
		public ColorF Color { get; set; } = ColorF.White;
		public Rectangle? Source { get; set; }
		public NineSlice? NineSlice { get; set; }
		public Vector2? Size { get; set; }
		public Vector2 Scale { get; set; } = Vector2.One;
		public float Rotation { get; set; }
		public Vector2 Origin { get; set; }
	}
}