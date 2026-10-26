using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using EdgeOfPlain.Client.Resource;
using EdgeOfPlain.Client.Resource.Textures;
using EdgeOfPlain.Core.Framework.Graphics;
using SFML.Graphics;
using SFML.System;
using SfColor = SFML.Graphics.Color;

namespace EdgeOfPlain.Client.Rendering;

public sealed class SpriteBatch(IRenderTarget target)
	: ISpriteBatch, IDisposable
{
	private const float DefaultQuadSize = 16f;
	private const int MaxNineSliceParts = 256;

	private readonly IRenderTarget _target = target ?? throw new ArgumentNullException(nameof(target));

	private readonly Dictionary<GroupKey, Bucket> _buckets = [];
	private readonly List<GroupKey> _activeKeys = [];
	private readonly List<TextEntry> _texts = [];
	private readonly Dictionary<uint, Text> _textCache = [];

	private Vector2 _pixels;
	private RectangleF? _cameraBounds;

	private GroupKey _lastKey;
	private Bucket? _lastBucket;

	public int SubmittedSprites { get; private set; }

	public int CulledSprites { get; private set; }

	public int DrawnSprites { get; private set; }

	public int DrawCalls { get; private set; }

	public int BatchCount => _activeKeys.Count;

	public void Begin(Camera2D? camera = null) => StartFrame(camera);

	private void StartFrame(Camera2D? camera)
	{
		foreach (var key in _activeKeys) _buckets[key].Reset();

		_activeKeys.Clear();
		_lastBucket = null;
		_texts.Clear();

		var size = _target.Size;
		_pixels = new Vector2(size.X, size.Y);

		_cameraBounds = camera?.CullingBounds(_pixels);
		ApplyCamera(camera);

		SubmittedSprites = 0;
		CulledSprites = 0;
		DrawnSprites = 0;
		DrawCalls = 0;
	}

	public void Draw(RenderMetadata metadata, float x, float y)
	{
		ArgumentNullException.ThrowIfNull(metadata);

		var texture = string.IsNullOrEmpty(metadata.Texture) ? null : ResolveTexture(metadata.Texture);
		var shader = string.IsNullOrEmpty(metadata.Shader) ? null : metadata.Shader;
		var bucket = ResolveBucket(new GroupKey(metadata.Layer, texture?.TexturePath, shader));

		if (metadata.NineSlice is { } slice)
		{
			AppendNineSlice(bucket, metadata, slice, x, y);

			return;
		}

		SubmittedSprites++;

		if (!TryBuildQuad(metadata, texture, x, y, out var quad))
		{
			CulledSprites++;
			return;
		}

		bucket.Append(quad, metadata.Color);
	}

	public void DrawText(string text, int size, float x, float y, uint layer)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

		if (string.IsNullOrEmpty(text)) return;

		_texts.Add(new TextEntry(text, (uint)size, x, y, layer, _texts.Count));
	}

	public Vector2 MeasureText(string text, int size)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

		if (string.IsNullOrEmpty(text)) return Vector2.Zero;

		var measure = ResolveText((uint)size);
		measure.DisplayedString = text;

		var bounds = measure.GetGlobalBounds();

		return new Vector2(bounds.Width, bounds.Height);
	}

	public void End()
	{
		switch (_activeKeys.Count)
		{
			case 0 when _texts.Count == 0:
				return;
			case > 1:
				_activeKeys.Sort(static (left, right) => Compare(left, right));
				break;
		}

		if (_texts.Count > 1) _texts.Sort(static (left, right) => CompareText(left, right));

		var textIndex = 0;

		for (var index = 0; index < _activeKeys.Count; index++)
		{
			var key = _activeKeys[index];

			while (textIndex < _texts.Count && _texts[textIndex].Layer < key.Layer)
				DrawTextItem(_texts[textIndex++]);

			DrawGroup(key);

			var lastOfLayer = index == _activeKeys.Count - 1 || _activeKeys[index + 1].Layer != key.Layer;

			if (!lastOfLayer) continue;

			while (textIndex < _texts.Count && _texts[textIndex].Layer == key.Layer)
				DrawTextItem(_texts[textIndex++]);
		}

		while (textIndex < _texts.Count) DrawTextItem(_texts[textIndex++]);

		_texts.Clear();
	}

	public void Dispose()
	{
		foreach (var text in _textCache.Values) text.Dispose();

		_textCache.Clear();
		_texts.Clear();
		_buckets.Clear();
		_activeKeys.Clear();
	}

	private Bucket ResolveBucket(in GroupKey key)
	{
		if (_lastBucket is not null && key.Equals(_lastKey)) return _lastBucket;

		if (!_buckets.TryGetValue(key, out var bucket))
		{
			bucket = new Bucket();
			_buckets[key] = bucket;
		}

		if (bucket.Count == 0) _activeKeys.Add(key);

		_lastKey = key;
		_lastBucket = bucket;

		return bucket;
	}

	private void AppendNineSlice(Bucket bucket, RenderMetadata metadata, NineSlice slice, float x, float y)
	{
		var destination = new RectangleF(
			0f,
			0f,
			metadata.Size?.X ?? slice.Source.Width,
			metadata.Size?.Y ?? slice.Source.Height);

		var effective = slice;
		if (effective.Mode == NineSliceMode.Tile && effective.TiledPartCount(destination) > MaxNineSliceParts)
			effective = effective with { Mode = NineSliceMode.Stretch };

		var count = effective.CountParts(destination);
		if (count == 0) return;

		Span<NineSlicePart> parts = stackalloc NineSlicePart[count];
		var filled = effective.SplitParts(destination, parts);

		float cos = 1f, sin = 0f;
		if (metadata.Rotation != 0f)
		{
			cos = MathF.Cos(metadata.Rotation);
			sin = MathF.Sin(metadata.Rotation);
		}

		for (var index = 0; index < filled; index++)
		{
			var part = parts[index];

			var quad = BuildQuad(
				part.Destination.Left - metadata.Origin.X,
				part.Destination.Top - metadata.Origin.Y,
				part.Destination.Right - metadata.Origin.X,
				part.Destination.Bottom - metadata.Origin.Y,
				x, y, cos, sin,
				part.Source.Left, part.Source.Top, part.Source.Right, part.Source.Bottom);

			SubmittedSprites++;

			if (IsOutside(quad))
			{
				CulledSprites++;
				continue;
			}

			bucket.Append(quad, metadata.Color);
		}
	}

	private static Quad BuildQuad(
		float left, float top, float right, float bottom,
		float x, float y, float cos, float sin,
		float texLeft, float texTop, float texRight, float texBottom) =>
		new(
			x + left * cos - top * sin,
			y + left * sin + top * cos,
			x + right * cos - top * sin,
			y + right * sin + top * cos,
			x + right * cos - bottom * sin,
			y + right * sin + bottom * cos,
			x + left * cos - bottom * sin,
			y + left * sin + bottom * cos,
			texLeft, texTop, texRight, texBottom);

	private bool IsOutside(in Quad quad)
	{
		if (_cameraBounds is not { } bounds) return false;

		var minX = MathF.Min(MathF.Min(quad.X0, quad.X1), MathF.Min(quad.X2, quad.X3));
		var maxX = MathF.Max(MathF.Max(quad.X0, quad.X1), MathF.Max(quad.X2, quad.X3));
		var minY = MathF.Min(MathF.Min(quad.Y0, quad.Y1), MathF.Min(quad.Y2, quad.Y3));
		var maxY = MathF.Max(MathF.Max(quad.Y0, quad.Y1), MathF.Max(quad.Y2, quad.Y3));

		return maxX < bounds.Left || minX > bounds.Right || maxY < bounds.Top || minY > bounds.Bottom;
	}

	private bool TryBuildQuad(RenderMetadata metadata, TextureData? texture, float x, float y, out Quad quad)
	{
		var source = metadata.Source ?? texture?.Source;
		var natural = source is { } region
			? new Vector2(region.Width, region.Height)
			: metadata.Size ?? new Vector2(DefaultQuadSize);
		var scale = ResolveScale(metadata, natural);

		var width = natural.X;
		var height = natural.Y;

		float cos = 1f, sin = 0f;
		if (metadata.Rotation != 0f)
		{
			cos = MathF.Cos(metadata.Rotation);
			sin = MathF.Sin(metadata.Rotation);
		}

		var texLeft = source?.Left ?? 0;
		var texTop = source?.Top ?? 0;

		quad = BuildQuad(
			-metadata.Origin.X,
			-metadata.Origin.Y,
			width * scale.X - metadata.Origin.X,
			height * scale.Y - metadata.Origin.Y,
			x, y, cos, sin,
			texLeft, texTop, texLeft + width, texTop + height);

		return !IsOutside(quad);
	}

	private void ApplyCamera(Camera2D? source)
	{
		var camera = source ?? new Camera2D(new Vector2(_target.Size.X, _target.Size.Y) / 2);
		var world = camera.WorldSize(_pixels);
		var current = _target.GetView();

		_target.SetView(new View(
			new Vector2f(camera.Position.X, camera.Position.Y),
			new Vector2f(world.X, world.Y))
		{
			Rotation = Angle.FromRadians(camera.Rotation),
			Viewport = current.Viewport,
			Scissor = current.Scissor,
		});
	}

	private void DrawGroup(in GroupKey key)
	{
		var bucket = _buckets[key];
		if (bucket.Count == 0) return;

		var states = RenderStates.Default;
		states.Texture = key.Texture is null ? null : ClientAssetsManager.Get<Texture>(key.Texture);
		states.Shader = key.Shader is null ? null : ResolveShader(key.Shader);

		_target.Draw(bucket.Vertices, 0, (uint)bucket.Count, PrimitiveType.Triangles, states);

		DrawCalls++;
		DrawnSprites += bucket.Count / 6;
	}

	private void DrawTextItem(in TextEntry entry)
	{
		var text = ResolveText(entry.Size);

		text.DisplayedString = entry.Content;
		text.Position = new Vector2f(entry.X, entry.Y);

		if (_cameraBounds is { } view)
		{
			var bounds = text.GetGlobalBounds();

			if (bounds.Left > view.Right || bounds.Left + bounds.Width < view.Left ||
			    bounds.Top > view.Bottom || bounds.Top + bounds.Height < view.Top)
				return;
		}

		_target.Draw(text, RenderStates.Default);
		DrawCalls++;
	}

	private Text ResolveText(uint size)
	{
		if (_textCache.TryGetValue(size, out var cached)) return cached;

		var text = new Text(GameContext.Fonts.Light, string.Empty, size);
		_textCache[size] = text;

		return text;
	}

	private static TextureData ResolveTexture(string key) => ClientAssetsManager.Get<TextureData>(key);

	private static Shader ResolveShader(string path) => ClientAssetsManager.Get<Shader>(path);

	private static Vector2 ResolveScale(RenderMetadata metadata, Vector2 natural)
	{
		if (metadata.Texture is null || metadata.Size is not { } size) return metadata.Scale;

		return new Vector2(
			natural.X > 0 ? size.X / natural.X * metadata.Scale.X : 0f,
			natural.Y > 0 ? size.Y / natural.Y * metadata.Scale.Y : 0f);
	}

	private static int Compare(in GroupKey left, in GroupKey right)
	{
		var result = left.Layer.CompareTo(right.Layer);
		if (result != 0) return result;

		result = string.CompareOrdinal(left.Texture, right.Texture);
		if (result != 0) return result;

		return string.CompareOrdinal(left.Shader, right.Shader);
	}

	private static int CompareText(in TextEntry left, in TextEntry right)
	{
		var result = left.Layer.CompareTo(right.Layer);

		return result != 0 ? result : left.Sequence.CompareTo(right.Sequence);
	}

	private sealed class Bucket
	{
		public Vertex[] Vertices = new Vertex[96];
		public int Count;

		public void Reset() => Count = 0;

		public void Append(in Quad quad, ColorF tint)
		{
			if (Count + 6 > Vertices.Length)
				Array.Resize(ref Vertices, Math.Max(Vertices.Length * 2, Count + 6));

			var color = new SfColor(tint.R, tint.G, tint.B, tint.A);

			var v0 = new Vertex(new Vector2f(quad.X0, quad.Y0), color, new Vector2f(quad.TexLeft, quad.TexTop));
			var v1 = new Vertex(new Vector2f(quad.X1, quad.Y1), color, new Vector2f(quad.TexRight, quad.TexTop));
			var v2 = new Vertex(new Vector2f(quad.X2, quad.Y2), color, new Vector2f(quad.TexRight, quad.TexBottom));
			var v3 = new Vertex(new Vector2f(quad.X3, quad.Y3), color, new Vector2f(quad.TexLeft, quad.TexBottom));

			Vertices[Count + 0] = v0;
			Vertices[Count + 1] = v1;
			Vertices[Count + 2] = v2;
			Vertices[Count + 3] = v0;
			Vertices[Count + 4] = v2;
			Vertices[Count + 5] = v3;

			Count += 6;
		}
	}

	private readonly record struct Quad(
		float X0,
		float Y0,
		float X1,
		float Y1,
		float X2,
		float Y2,
		float X3,
		float Y3,
		float TexLeft,
		float TexTop,
		float TexRight,
		float TexBottom);

	private readonly record struct TextEntry(
		string Content,
		uint Size,
		float X,
		float Y,
		uint Layer,
		int Sequence);

	private readonly record struct GroupKey(uint Layer, string? Texture, string? Shader);
}
