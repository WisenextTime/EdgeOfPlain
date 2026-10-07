namespace EdgeOfPlain.Core.Framework.Graphics;

public readonly record struct ColorF(byte R, byte G, byte B, byte A)
{
	public static ColorF White => new(255, 255, 255, 255);

	public static ColorF FromRgb(uint rgb) => new(
		(byte)(rgb >> 16 & 0xFF),
		(byte)(rgb >> 8 & 0xFF),
		(byte)(rgb & 0xFF),
		255);
}