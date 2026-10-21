using System;
using System.Numerics;
using EdgeOfPlain.Client.Rendering;
using EdgeOfPlain.Client.Resource;
using EdgeOfPlain.Core.Framework.Graphics;
using SFML.System;
using Xunit;

namespace EdgeOfPlain.Client.Tests;

public sealed class SpriteBatchTests : IDisposable
{
	private readonly UiFonts _fonts = new();
	private readonly FakeRenderTarget _contextTarget = new();
	private readonly FakeGameWindow _window = new();

	public SpriteBatchTests() => GameContext.Initialize(_window, _fonts, _contextTarget);

	public void Dispose()
	{
		ClientAssetsManager.Dispose();
		_fonts.Dispose();
	}

	[Fact]
	public void DrawBatchesQuadsWithSameLayer()
	{
		var target = new FakeRenderTarget();
		using var batch = new SpriteBatch(target);
		var metadata = new RenderMetadata
		{
			Layer = 1,
			Size = new Vector2(10, 10),
		};

		batch.Begin();
		batch.Draw(metadata, 0, 0);
		batch.Draw(metadata, 20, 0);
		batch.End();

		Assert.Equal(1, batch.DrawCalls);
		Assert.Equal(2, batch.DrawnSprites);
		Assert.Equal(0, batch.CulledSprites);

		var draw = Assert.Single(target.VertexBatches);
		Assert.Equal(12, draw.Vertices.Length);
	}

	[Fact]
	public void DifferentLayersProduceSeparateDrawCalls()
	{
		var target = new FakeRenderTarget();
		using var batch = new SpriteBatch(target);
		var lower = new RenderMetadata { Layer = 1, Size = new Vector2(8, 8) };
		var upper = new RenderMetadata { Layer = 2, Size = new Vector2(8, 8) };

		batch.Begin();
		batch.Draw(lower, 0, 0);
		batch.Draw(upper, 0, 0);
		batch.End();

		Assert.Equal(2, batch.DrawCalls);
		Assert.Equal(2, target.VertexBatches.Count);
	}

	[Fact]
	public void OriginAndScaleAreAppliedToQuadVertices()
	{
		var target = new FakeRenderTarget();
		using var batch = new SpriteBatch(target);
		var metadata = new RenderMetadata
		{
			Layer = 1,
			Size = new Vector2(10, 20),
			Scale = new Vector2(2, 2),
			Origin = new Vector2(5, 10),
		};

		batch.Begin();
		batch.Draw(metadata, 100, 100);
		batch.End();

		var vertices = Assert.Single(target.VertexBatches).Vertices;

		Assert.Equal(90f, vertices[0].Position.X, 3);
		Assert.Equal(80f, vertices[0].Position.Y, 3);
		Assert.Equal(110f, vertices[1].Position.X, 3);
		Assert.Equal(80f, vertices[1].Position.Y, 3);
		Assert.Equal(110f, vertices[2].Position.X, 3);
		Assert.Equal(120f, vertices[2].Position.Y, 3);
	}

	[Fact]
	public void CameraCullsSpritesOutsideTheView()
	{
		var target = new FakeRenderTarget { Size = new Vector2u(100, 100) };
		var camera = new Camera2D(new Vector2(0, 0));
		using var batch = new SpriteBatch(target);
		var metadata = new RenderMetadata { Layer = 1, Size = new Vector2(10, 10) };

		batch.Begin(camera);
		batch.Draw(metadata, 1000, 1000);
		batch.End();

		Assert.Equal(1, batch.CulledSprites);
		Assert.Empty(target.VertexBatches);
	}

	[Fact]
	public void DrawTextIsQueuedAndDrawn()
	{
		var target = new FakeRenderTarget();
		using var batch = new SpriteBatch(target);

		batch.Begin();
		batch.DrawText("hello", 16, 10, 20, 1);
		batch.End();

		Assert.Single(target.Drawables);
		Assert.Equal(1, batch.DrawCalls);
	}

	[Fact]
	public void BeginResetsCounters()
	{
		var target = new FakeRenderTarget();
		using var batch = new SpriteBatch(target);
		var metadata = new RenderMetadata { Layer = 1, Size = new Vector2(10, 10) };

		batch.Begin();
		batch.Draw(metadata, 0, 0);
		batch.End();

		batch.Begin();
		batch.End();

		Assert.Equal(0, batch.SubmittedSprites);
		Assert.Equal(0, batch.CulledSprites);
		Assert.Equal(0, batch.DrawnSprites);
		Assert.Equal(0, batch.DrawCalls);
	}

	[Fact]
	public void SeparateBatchesCanUseDifferentCameras()
	{
		var target = new FakeRenderTarget { Size = new Vector2u(100, 100) };
		using var world = new SpriteBatch(target);
		using var hud = new SpriteBatch(target);
		var metadata = new RenderMetadata { Layer = 1, Size = new Vector2(10, 10) };

		world.Begin(new Camera2D(Vector2.Zero, 2f));
		world.Draw(metadata, 0, 0);
		world.End();

		Assert.Equal(50f, target.GetView().Size.X, 3);
		Assert.Equal(50f, target.GetView().Size.Y, 3);

		hud.Begin(new Camera2D(Vector2.Zero));
		hud.Draw(metadata, 0, 0);
		hud.End();

		Assert.Equal(100f, target.GetView().Size.X, 3);
		Assert.Equal(100f, target.GetView().Size.Y, 3);
	}
}
