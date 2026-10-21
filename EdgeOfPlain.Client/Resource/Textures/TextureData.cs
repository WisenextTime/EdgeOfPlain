using System.Drawing;
using System.Numerics;
using SFML.Graphics;

namespace EdgeOfPlain.Client.Resource.Textures;

public sealed class TextureData
{
	internal TextureData(string spriteId, string texturePath, Rectangle source, RectangleF uv)
	{
		SpriteId = spriteId;
		TexturePath = texturePath;
		Source = source;
		Uv = uv;
	}

	public string SpriteId { get; }

	public string TexturePath { get; }

	public string Key => MakeKey(TexturePath, SpriteId);

	public Rectangle Source { get; }

	public RectangleF Uv { get; }

	public Vector2 Size => new(Source.Width, Source.Height);

	public SFML.Graphics.Texture Texture => ClientAssetsManager.Get<Texture>(TexturePath);

	public static string MakeKey(string texturePath, string spriteId) => $"{texturePath}#{spriteId}";
}
