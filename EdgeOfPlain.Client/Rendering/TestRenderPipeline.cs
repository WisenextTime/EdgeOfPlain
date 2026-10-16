using System;
using System.Collections.Generic;
using System.Numerics;
using EdgeOfPlain.Core.Framework.Graphics;
using EdgeOfPlain.Core.Framework.Resource;
using SFML.Graphics;
using SFML.System;

namespace EdgeOfPlain.Client.Rendering;

public sealed class TestRenderPipeline(IRenderTarget target) : IRenderPipeline
{
	private const float DefaultQuadSize = 16f;

	private readonly Dictionary<string, Texture> _textures = new();
	private readonly List<DrawEntry> _pending = [];

	public void Begin() => _pending.Clear();

	public void Draw(RenderMetadata metadata, float x, float y)
		=> _pending.Add(new DrawEntry(metadata, new Vector2f(x, y)));

	public void End()
	{
		_pending.Sort(static (a, b) => a.Metadata.Layer.CompareTo(b.Metadata.Layer));

		foreach (var entry in _pending) Render(entry);
	}

	private void Render(DrawEntry entry)
	{
		var metadata = entry.Metadata;

		if (metadata.Texture is not null)
		{
			var texture = ResolveTexture(metadata.Texture);
			var sprite = new Sprite(texture)
			{
				Position = entry.Position,
				Color = ToColor(metadata.Color),
				Origin = ToVector(metadata.Origin),
				Scale = ResolveSpriteScale(metadata, NaturalSize(metadata, texture)),
				Rotation = ToDegrees(metadata.Rotation),
			};

			if (metadata.Source is { } source)
				sprite.TextureRect = new IntRect(
					new Vector2i(source.Left, source.Top), new Vector2i(source.Width, source.Height));

			target.Draw(sprite, RenderStates.Default);
			return;
		}

		var quad = new RectangleShape(ToVector(ResolveQuadSize(metadata)))
		{
			Position = entry.Position,
			FillColor = ToColor(metadata.Color),
			Origin = ToVector(metadata.Origin),
			Scale = ToVector(metadata.Scale),
			Rotation = ToDegrees(metadata.Rotation),
		};

		target.Draw(quad, RenderStates.Default);
	}

	private Texture ResolveTexture(string path)
	{
		if (_textures.TryGetValue(path, out var cached)) return cached;

		var texture = AssetsManager.LoadResource<Texture>(path);
		_textures[path] = texture;
		return texture;
	}

	private static Vector2 ResolveQuadSize(RenderMetadata metadata)
		=> metadata.Size
		   ?? (metadata.Source is { } source ? new Vector2(source.Width, source.Height) : new Vector2(DefaultQuadSize));

	private static Vector2 NaturalSize(RenderMetadata metadata, Texture texture)
		=> metadata.Source is { } source
			? new Vector2(source.Width, source.Height)
			: new Vector2(texture.Size.X, texture.Size.Y);

	private static Vector2f ResolveSpriteScale(RenderMetadata metadata, Vector2 natural)
	{
		if (metadata.Size is not { } size) return ToVector(metadata.Scale);

		return new Vector2f(
			natural.X > 0 ? size.X / natural.X * metadata.Scale.X : 0f,
			natural.Y > 0 ? size.Y / natural.Y * metadata.Scale.Y : 0f);
	}

	private static Color ToColor(ColorF color) => new(color.R, color.G, color.B, color.A);

	private static Vector2f ToVector(Vector2 value) => new(value.X, value.Y);

	private static float ToDegrees(float radians) => radians * (180f / MathF.PI);

	private readonly record struct DrawEntry(RenderMetadata Metadata, Vector2f Position);
}
