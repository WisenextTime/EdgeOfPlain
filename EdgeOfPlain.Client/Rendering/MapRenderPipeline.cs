using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using EdgeOfPlain.Client.Resource;
using EdgeOfPlain.Client.Resource.Textures;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Framework.Graphics;
using EdgeOfPlain.Core.Game;
using SFML.Graphics;
using SFML.Graphics.Glsl;
using SFML.System;
using SfColor = SFML.Graphics.Color;

namespace EdgeOfPlain.Client.Rendering;

public readonly record struct TileChunk(uint FirstVertex, uint VertexCount, RectangleF Bounds);

public sealed class TileMaterial(TextureData textureData, IRenderTarget target) : IDisposable
{
	private readonly IRenderTarget _target = target ?? throw new ArgumentNullException(nameof(target));
	private readonly List<Vertex> _vertices = [];
	private readonly Dictionary<Vector2, TileChunk> _chunks = [];
	private VertexBuffer? _buffer;
	private Vector2 _chunkPosition;
	private RectangleF _chunkBounds;
	private uint _chunkStart;

	public TextureData TextureData { get; } = textureData ?? throw new ArgumentNullException(nameof(textureData));
	public Texture Texture => TextureData.Texture;
	public int VertexCount => _vertices.Count;
	public IReadOnlyDictionary<Vector2, TileChunk> Chunks => _chunks;

	public RectangleF MapUv(RectangleF local)
		=> new(
			TextureData.Uv.Left + local.Left * TextureData.Uv.Width,
			TextureData.Uv.Top + local.Top * TextureData.Uv.Height,
			local.Width * TextureData.Uv.Width,
			local.Height * TextureData.Uv.Height);

	public void BeginChunk(Vector2 position, RectangleF bounds)
	{
		_chunkPosition = position;
		_chunkBounds = bounds;
		_chunkStart = (uint)_vertices.Count;
	}

	public void EndChunk()
	{
		var count = (uint)_vertices.Count - _chunkStart;

		if (count > 0)
			_chunks.Add(_chunkPosition, new TileChunk(_chunkStart, count, _chunkBounds));
	}

	public void AddTile(int tileX, int tileY, MapTile tile)
	{
		var tileSize = GameMap.TileSize;
		var left = tileX * (float)tileSize;
		var top = tileY * (float)tileSize;
		var right = left + tileSize;
		var bottom = top + tileSize;
		var uv = MapUv(tile.Uv);

		AddVertex(right, top, uv.Right, uv.Top);
		AddVertex(right, bottom, uv.Right, uv.Bottom);
		AddVertex(left, top, uv.Left, uv.Top);
		AddVertex(right, bottom, uv.Right, uv.Bottom);
		AddVertex(left, bottom, uv.Left, uv.Bottom);
		AddVertex(left, top, uv.Left, uv.Top);
	}

	public void Upload()
	{
		if (_vertices.Count == 0)
			return;

		_buffer?.Dispose();
		_buffer = new VertexBuffer(
			(uint)_vertices.Count,
			PrimitiveType.Triangles,
			VertexBuffer.UsageSpecifier.Dynamic);
		_buffer.Update([.. _vertices]);
	}

	public void Begin(Shader? shader, RectangleF? cameraBounds)
	{
		if (_buffer is null || _chunks.Count == 0) return;

		var states = RenderStates.Default;
		states.Shader = shader;
		states.Texture = Texture;

		foreach (var chunk in _chunks.Values)
		{
			if (cameraBounds is { } bounds && !Intersects(bounds, chunk.Bounds)) continue;

			_buffer.Draw(_target, chunk.FirstVertex, chunk.VertexCount, states);
		}
	}

	public void Dispose() => _buffer?.Dispose();

	private static bool Intersects(RectangleF left, RectangleF right)
		=> left.Left < right.Right &&
		   left.Right > right.Left &&
		   left.Top < right.Bottom &&
		   left.Bottom > right.Top;

	private void AddVertex(float x, float y, float u, float v)
		=> _vertices.Add(new Vertex(new Vector2f(x, y), SfColor.White, new Vector2f(u, v)));
}

public sealed class MapRenderPipeline : IRenderPipeline, IDisposable
{
	private readonly IRenderTarget _target;
	private readonly List<TileRenderer> _renderers;
	private readonly Dictionary<string, TileRenderer> _renderersById;
	private readonly TileRenderer _defaultRenderer;

	public MapRenderPipeline(GameMap map) : this(map, GameContext.RenderTarget) { }

	public MapRenderPipeline(GameMap map, IRenderTarget target)
	{
		ArgumentNullException.ThrowIfNull(map);

		_target = target ?? throw new ArgumentNullException(nameof(target));
		_renderers = new List<TileRenderer>(map.Renderers.Count);
		_renderersById = new Dictionary<string, TileRenderer>(StringComparer.Ordinal);

		foreach (var definition in map.Renderers)
		{
			var renderer = new TileRenderer(definition, _target);
			_renderers.Add(renderer);
			_renderersById.Add(definition.Id, renderer);
		}

		_defaultRenderer = new TileRenderer(null, _target);
		Build(map);
	}

	public IReadOnlyList<TileRenderer> Renderers => _renderers;

	public void Begin(Camera2D? camera2D)
	{
		ApplyCamera(camera2D);

		foreach (var renderer in _renderers)
			renderer.Begin(camera2D);
		_defaultRenderer.Begin(camera2D);
	}

	public void End()
	{
		foreach (var renderer in _renderers)
			renderer.End();
		_defaultRenderer.End();
	}

	private void Build(GameMap map)
	{
		foreach (var mapChunk in map.Chunks)
		{
			foreach (var renderer in _renderers)
				renderer.BeginChunk(mapChunk.Position, mapChunk.Bounds);
			_defaultRenderer.BeginChunk(mapChunk.Position, mapChunk.Bounds);

			for (var localY = 0; localY < mapChunk.Rows; localY++)
			for (var localX = 0; localX < mapChunk.Columns; localX++)
			{
				var tileX = mapChunk.TileX + localX;
				var tileY = mapChunk.TileY + localY;
				var tile = map.GetTile(tileX, tileY);

				if (tile.IsEmpty) continue;

				var renderer = ResolveRenderer(tile);
				renderer.AddTile(tileX, tileY, tile);
			}

			foreach (var renderer in _renderers)
				renderer.EndChunk();
			_defaultRenderer.EndChunk();
		}

		foreach (var renderer in _renderers)
			renderer.Upload();
		_defaultRenderer.Upload();
	}

	private TileRenderer ResolveRenderer(MapTile tile)
	{
		if (tile.Renderer is { } definition && _renderersById.TryGetValue(definition.Id, out var renderer))
			return renderer;

		return _defaultRenderer;
	}

	private void ApplyCamera(Camera2D? source)
	{
		var pixels = new Vector2(_target.Size.X, _target.Size.Y);
		var camera = source ?? new Camera2D(pixels / 2f);
		var world = camera.WorldSize(pixels);
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

	public void Dispose()
	{
		foreach (var renderer in _renderers)
			renderer.Dispose();
		_defaultRenderer.Dispose();
	}
}

public sealed class TileRenderer : IDisposable
{
	private readonly IRenderTarget _target;
	private readonly List<TileMaterial> _materials = [];
	private readonly Dictionary<string, TileMaterial> _materialsByTexture = [];
	private Vector2 _chunkPosition;
	private RectangleF _chunkBounds;
	private RectangleF? _cameraBounds;

	public TileRenderer(TileRendererDef? definition, IRenderTarget? target = null)
	{
		Definition = definition;
		_target = target ?? GameContext.RenderTarget ??
			throw new InvalidOperationException("A render target is required for a tile renderer.");

		if (definition is null)
		{
			Shader = ClientAssetsManager.Get<Shader>("assets://base/Shaders/defaultTile.glsl");
			return;
		}

		if (string.IsNullOrWhiteSpace(definition.ShaderPath)) return;
		Shader = ClientAssetsManager.Get<Shader>(definition.ShaderKey);
		ApplyUniforms(Shader, definition.Uniforms);
	}

	public TileRendererDef? Definition { get; }
	public Shader? Shader { get; }

	public IReadOnlyList<TileMaterial> Materials => _materials;

	public void BeginChunk(Vector2 position, RectangleF bounds)
	{
		_chunkPosition = position;
		_chunkBounds = bounds;

		foreach (var material in _materials)
			material.BeginChunk(position, bounds);
	}

	public void EndChunk()
	{
		foreach (var material in _materials)
			material.EndChunk();
	}

	public void AddTile(int tileX, int tileY, MapTile tile)
	{
		if (tile.Tileset is null)
			throw new InvalidOperationException($"Tile ({tileX},{tileY}) has no tileset material.");

		var textureKey = tile.Tileset.TextureKey;

		if (!_materialsByTexture.TryGetValue(textureKey, out var material))
		{
			if (!ClientAssetsManager.TryGet<TextureData>(textureKey, out var texture))
				throw new InvalidOperationException(
					$"Tileset '{tile.Tileset.Definition.Id}' references missing texture '{textureKey}'.");

			material = new TileMaterial(texture, _target);
			material.BeginChunk(_chunkPosition, _chunkBounds);

			_materialsByTexture.Add(textureKey, material);
			_materials.Add(material);
		}

		material.AddTile(tileX, tileY, tile);
	}

	public void Upload()
	{
		foreach (var material in _materials)
			material.Upload();
	}

	public void Begin(Camera2D? camera2D)
	{
		var pixels = new Vector2(_target.Size.X, _target.Size.Y);
		_cameraBounds = camera2D?.CullingBounds(pixels);

		if (Shader is { } shader && camera2D is { } camera)
		{
			var world = camera.WorldSize(pixels);
			shader.SetUniform("u_cameraPosition", new Vec2(camera.Position.X, camera.Position.Y));
			shader.SetUniform("u_cameraSize", new Vec2(world.X, world.Y));
			shader.SetUniform("u_cameraRotation", camera.Rotation);

			if (Definition?.Id is null or "randomTileColor")
				shader.SetUniform("u_zoom", camera.Zoom);
		}

		foreach (var material in _materials)
			material.Begin(Shader, _cameraBounds);
	}

	public void End()
	{
	}

	public void Dispose()
	{
		foreach (var material in _materials)
			material.Dispose();
	}

	private void ApplyUniforms(Shader shader, IReadOnlyDictionary<string, ShaderUniform> uniforms)
	{
		foreach (var (name, uniform) in uniforms)
		{
			switch (uniform.Type)
			{
				case ShaderUniform.UniformType.Float:
					shader.SetUniform(name, Values(name, uniform, 1)[0]);
					break;

				case ShaderUniform.UniformType.Int:
					shader.SetUniform(name, (int)Values(name, uniform, 1)[0]);
					break;

				case ShaderUniform.UniformType.Bool:
					shader.SetUniform(name, Values(name, uniform, 1)[0] != 0f);
					break;

				case ShaderUniform.UniformType.Vec2:
					var vec2 = Values(name, uniform, 2);
					shader.SetUniform(name, new Vec2(vec2[0], vec2[1]));
					break;

				case ShaderUniform.UniformType.Vec3:
					var vec3 = Values(name, uniform, 3);
					shader.SetUniform(name, new Vec3(vec3[0], vec3[1], vec3[2]));
					break;

				case ShaderUniform.UniformType.Vec4:
					var vec4 = Values(name, uniform, 4);
					shader.SetUniform(name, new Vec4(vec4[0], vec4[1], vec4[2], vec4[3]));
					break;

				case ShaderUniform.UniformType.Mat3:
					var mat3 = Values(name, uniform, 9);
					shader.SetUniform(name, new Mat3(
						mat3[0], mat3[1], mat3[2],
						mat3[3], mat3[4], mat3[5],
						mat3[6], mat3[7], mat3[8]));
					break;

				case ShaderUniform.UniformType.Mat4:
					var mat4 = Values(name, uniform, 16);
					shader.SetUniform(name, new Mat4(
						mat4[0], mat4[1], mat4[2], mat4[3],
						mat4[4], mat4[5], mat4[6], mat4[7],
						mat4[8], mat4[9], mat4[10], mat4[11],
						mat4[12], mat4[13], mat4[14], mat4[15]));
					break;

				case ShaderUniform.UniformType.Sampler2D:
					if (string.IsNullOrWhiteSpace(uniform.Texture))
						throw new InvalidOperationException($"Shader uniform '{name}' requires a texture path.");

					shader.SetUniform(name,
						ClientAssetsManager.Get<Texture>(Definition!.ResolveResourcePath(uniform.Texture)));
					break;

				default:
					throw new InvalidOperationException($"Unsupported shader uniform type '{uniform.Type}'.");
			}
		}
	}

	private static float[] Values(string name, in ShaderUniform uniform, int expected)
	{
		var values = uniform.Value;

		if (values is null || values.Length != expected)
			throw new InvalidOperationException(
				$"Shader uniform '{name}' requires {expected} value(s), got {values?.Length ?? 0}.");

		return values;
	}
}
