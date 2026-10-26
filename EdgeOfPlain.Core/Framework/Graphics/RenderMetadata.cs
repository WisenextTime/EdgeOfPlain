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
	public NineSlice? NineSlice { get; set; }
	public Vector2? Size { get; set; }
	public Vector2 Scale { get; set; } = Vector2.One;
	public float Rotation { get; set; }

	/// <summary>
	/// 绘制原点（旋转与定位的基准点），单位为绘制后的目标尺寸：<see cref="Size"/> × <see cref="Scale"/>。
	/// 设成目标尺寸的一半即为“以中心对齐”，例如 <c>Size = (40, 40)</c> 时取 <c>(20, 20)</c>。
	/// 与 <see cref="NineSlice"/> 路径的语义一致。
	/// </summary>
	public Vector2 Origin { get; set; }
}