using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using EdgeOfPlain.Client.Resource;
using EdgeOfPlain.Core.Framework.Graphics;
using EdgeOfPlain.Core.Framework.Resource;
using SFML.Graphics;
using SFML.System;
using SfColor = SFML.Graphics.Color;

namespace EdgeOfPlain.Client.Rendering;
public sealed class SpriteBatch(IRenderTarget target, UiFonts fonts, Func<Camera2D>? camera = null)
	: ISpriteBatch, IDisposable
{
	private const float DefaultQuadSize = 16f;

	private readonly IRenderTarget _target = target ?? throw new ArgumentNullException(nameof(target));
	private readonly UiFonts _fonts = fonts ?? throw new ArgumentNullException(nameof(fonts));
	private readonly Func<string, Shader> _shaderLoader = static path => AssetsManager.LoadResource<Shader>(path);

	private readonly Dictionary<string, Texture> _textures = new(StringComparer.Ordinal);
	private readonly Dictionary<string, Shader> _shaders = new(StringComparer.Ordinal);
	private readonly Dictionary<Camera2D, int> _cameraSlots = [];
	private readonly List<Camera2D> _cameras = [];
	private readonly List<RectangleF> _cameraBounds = [];

	private readonly Dictionary<GroupKey, Bucket> _buckets = [];
	private readonly List<GroupKey> _activeKeys = [];
	private readonly List<TextEntry> _texts = [];
	private readonly Dictionary<uint, Text> _textCache = [];

	private Vector2 _pixels;
	private Camera2D? _appliedCamera;

	private Camera2D? _lastCamera;
	private int _lastCameraSlot = -1;
	private string? _lastTexturePath;
	private Texture? _lastTexture;
	private GroupKey _lastKey;
	private Bucket? _lastBucket;

	public int SubmittedSprites { get; private set; }

	public int CulledSprites { get; private set; }

	public int DrawnSprites { get; private set; }

	public int DrawCalls { get; private set; }

	public int BatchCount => _activeKeys.Count;

	public void Begin()
	{
		foreach (var key in _activeKeys) _buckets[key].Reset();

		_activeKeys.Clear();
		_cameraSlots.Clear();
		_cameras.Clear();
		_cameraBounds.Clear();
		_appliedCamera = null;

		_lastCamera = null;
		_lastCameraSlot = -1;
		_lastTexturePath = null;
		_lastTexture = null;
		_lastBucket = null;

		_texts.Clear();

		var size = _target.Size;
		_pixels = new Vector2(size.X, size.Y);

		SubmittedSprites = 0;
		CulledSprites = 0;
		DrawnSprites = 0;
		DrawCalls = 0;
	}

	public void Draw(RenderMetadata metadata, float x, float y)
	{
		ArgumentNullException.ThrowIfNull(metadata);

		SubmittedSprites++;

		var cameraSlot = -1;
		RectangleF? viewBounds = null;

		if (camera is not null)
		{
			var activeCamera = camera();

			if (_lastCamera is { } cached && cached.Equals(activeCamera))
			{
				cameraSlot = _lastCameraSlot;
			}
			else
			{
				cameraSlot = SlotFor(activeCamera);
				_lastCamera = activeCamera;
				_lastCameraSlot = cameraSlot;
			}

			viewBounds = _cameraBounds[cameraSlot];
		}

		var texture = metadata.Texture is null ? null : ResolveTexture(metadata.Texture);

		if (!TryBuildQuad(metadata, texture, x, y, viewBounds, out var quad))
		{
			CulledSprites++;
			return;
		}

		var key = new GroupKey(metadata.Layer, metadata.Texture, metadata.Shader, cameraSlot);

		Bucket bucket;
		if (_lastBucket is not null && key.Equals(_lastKey))
		{
			bucket = _lastBucket;
		}
		else
		{
			if (!_buckets.TryGetValue(key, out var existing))
			{
				existing = new Bucket();
				_buckets[key] = existing;
			}

			bucket = existing;
			_lastKey = key;
			_lastBucket = bucket;
		}

		if (bucket.Count == 0) _activeKeys.Add(key);

		bucket.Append(quad, metadata.Color);
	}

	public void DrawText(string text, int size, float x, float y, uint layer)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

		if (string.IsNullOrEmpty(text)) return;

		var cameraSlot = camera is null ? -1 : SlotFor(camera());
		_texts.Add(new TextEntry(text, (uint)size, x, y, layer, cameraSlot, _texts.Count));
	}

	public void End()
	{
		if (_activeKeys.Count == 0 && _texts.Count == 0) return;

		if (_activeKeys.Count > 1) _activeKeys.Sort(static (left, right) => Compare(left, right));
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
		foreach (var shader in _shaders.Values) shader.Dispose();
		foreach (var texture in _textures.Values) texture.Dispose();

		_textCache.Clear();
		_texts.Clear();
		_shaders.Clear();
		_textures.Clear();
		_buckets.Clear();
		_activeKeys.Clear();
	}

	private int SlotFor(Camera2D camera2D)
	{
		if (_cameraSlots.TryGetValue(camera2D, out var slot)) return slot;

		slot = _cameras.Count;
		_cameraSlots[camera2D] = slot;
		_cameras.Add(camera2D);
		_cameraBounds.Add(camera2D.CullingBounds(_pixels));

		return slot;
	}

	private bool TryBuildQuad(RenderMetadata metadata, Texture? texture, float x, float y, RectangleF? view, out Quad quad)
	{
		var natural = NaturalSize(metadata, texture);
		var scale = ResolveScale(metadata, natural);

		var width = natural.X;
		var height = natural.Y;

		float cos = 1f, sin = 0f;
		if (metadata.Rotation != 0f)
		{
			cos = MathF.Cos(metadata.Rotation);
			sin = MathF.Sin(metadata.Rotation);
		}

		var left = -metadata.Origin.X * scale.X;
		var top = -metadata.Origin.Y * scale.Y;
		var right = (width - metadata.Origin.X) * scale.X;
		var bottom = (height - metadata.Origin.Y) * scale.Y;

		var x0 = x + left * cos - top * sin;
		var y0 = y + left * sin + top * cos;
		var x1 = x + right * cos - top * sin;
		var y1 = y + right * sin + top * cos;
		var x2 = x + right * cos - bottom * sin;
		var y2 = y + right * sin + bottom * cos;
		var x3 = x + left * cos - bottom * sin;
		var y3 = y + left * sin + bottom * cos;

		if (view is { } bounds)
		{
			var minX = MathF.Min(MathF.Min(x0, x1), MathF.Min(x2, x3));
			var maxX = MathF.Max(MathF.Max(x0, x1), MathF.Max(x2, x3));
			var minY = MathF.Min(MathF.Min(y0, y1), MathF.Min(y2, y3));
			var maxY = MathF.Max(MathF.Max(y0, y1), MathF.Max(y2, y3));

			if (maxX < bounds.Left || minX > bounds.Right || maxY < bounds.Top || minY > bounds.Bottom)
			{
				quad = default;
				return false;
			}
		}

		var texLeft = metadata.Source?.Left ?? 0;
		var texTop = metadata.Source?.Top ?? 0;

		quad = new Quad(
			x0, y0, x1, y1, x2, y2, x3, y3,
			texLeft, texTop, texLeft + width, texTop + height);

		return true;
	}

	private void ApplyCamera(int slot)
	{
		if (slot < 0 || slot >= _cameras.Count) return;

		var camera2D = _cameras[slot];
		if (_appliedCamera is { } applied && applied.Equals(camera2D)) return;

		_appliedCamera = camera2D;

		var world = camera2D.WorldSize(_pixels);
		var current = _target.GetView();

		_target.SetView(new View(
			new Vector2f(camera2D.Position.X, camera2D.Position.Y),
			new Vector2f(world.X, world.Y))
		{
			Rotation = Angle.FromRadians(camera2D.Rotation),
			Viewport = current.Viewport,
			Scissor = current.Scissor,
		});
	}

	private void DrawGroup(in GroupKey key)
	{
		var bucket = _buckets[key];
		if (bucket.Count == 0) return;

		ApplyCamera(key.CameraSlot);

		var states = RenderStates.Default;
		states.Texture = key.Texture is null ? null : ResolveTexture(key.Texture);
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

		if (entry.CameraSlot >= 0)
		{
			var bounds = text.GetGlobalBounds();
			var view = _cameraBounds[entry.CameraSlot];

			if (bounds.Left > view.Right || bounds.Left + bounds.Width < view.Left ||
				bounds.Top > view.Bottom || bounds.Top + bounds.Height < view.Top)
				return;

			ApplyCamera(entry.CameraSlot);
		}

		_target.Draw(text, RenderStates.Default);
		DrawCalls++;
	}

	private Text ResolveText(uint size)
	{
		if (_textCache.TryGetValue(size, out var cached)) return cached;

		var text = new Text(_fonts.Light, string.Empty, size);
		_textCache[size] = text;

		return text;
	}

	private Texture ResolveTexture(string path)
	{
		if (_lastTexture is not null && string.Equals(_lastTexturePath, path, StringComparison.Ordinal)) return _lastTexture;

		if (!_textures.TryGetValue(path, out var texture))
		{
			texture = AssetsManager.LoadResource<Texture>(path);
			_textures[path] = texture;
		}

		_lastTexturePath = path;
		_lastTexture = texture;

		return texture;
	}

	private Shader ResolveShader(string path)
	{
		if (_shaders.TryGetValue(path, out var cached)) return cached;

		var shader = _shaderLoader(path);
		_shaders[path] = shader;
		return shader;
	}

	private static Vector2 NaturalSize(RenderMetadata metadata, Texture? texture)
	{
		if (metadata.Source is { } source) return new Vector2(source.Width, source.Height);
		if (texture is not null) return new Vector2(texture.Size.X, texture.Size.Y);

		return metadata.Size ?? new Vector2(DefaultQuadSize);
	}

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

		result = string.CompareOrdinal(left.Shader, right.Shader);
		if (result != 0) return result;

		return left.CameraSlot.CompareTo(right.CameraSlot);
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
		float X0, float Y0,
		float X1, float Y1,
		float X2, float Y2,
		float X3, float Y3,
		float TexLeft, float TexTop,
		float TexRight, float TexBottom);

	private readonly record struct TextEntry(string Content, uint Size, float X, float Y, uint Layer, int CameraSlot, int Sequence);

	private readonly record struct GroupKey(uint Layer, string? Texture, string? Shader, int CameraSlot);
}
