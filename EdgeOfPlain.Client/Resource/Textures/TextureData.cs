using System;
using System.Drawing;
using System.Numerics;
using SFML.Graphics;
using Zio;

namespace EdgeOfPlain.Client.Resource.Textures;

public sealed class TextureData
{
	internal TextureData(string spriteId, UPath texturePath, Rectangle source, RectangleF uv)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(spriteId);

		if (texturePath.IsNull || texturePath.IsEmpty)
			throw new System.ArgumentException("A texture path is required.", nameof(texturePath));

		SpriteId = spriteId;
		TexturePath = texturePath;
		Source = source;
		Uv = uv;
	}

	public string SpriteId { get; }

	public UPath TexturePath { get; }

	public UPath Key => MakeKey(TexturePath, SpriteId);

	public Rectangle Source { get; }

	public RectangleF Uv { get; }

	public Vector2 Size => new(Source.Width, Source.Height);

	public Texture Texture => ClientAssetsManager.Get<Texture>(TexturePath);

	public static UPath MakeKey(UPath texturePath, string spriteId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(spriteId);

		return new UPath($"{texturePath.FullName}#{spriteId}");
	}
}