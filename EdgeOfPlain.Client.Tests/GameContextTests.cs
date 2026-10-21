using System;
using System.Numerics;
using EdgeOfPlain.Client.Resource;
using EdgeOfPlain.Core.Framework.Graphics;
using SFML.System;
using Xunit;

namespace EdgeOfPlain.Client.Tests;

public sealed class GameContextTests : IDisposable
{
	private readonly UiFonts _fonts = new();
	private readonly FakeRenderTarget _target = new() { Size = new Vector2u(640, 480) };
	private readonly FakeGameWindow _window = new();

	public GameContextTests() => GameContext.Initialize(_window, _fonts, _target);

	public void Dispose()
	{
		ClientAssetsManager.Dispose();
		_fonts.Dispose();
	}

	[Fact]
	public void WorldCameraStartsFromWindowCamera()
	{
		Assert.Equal(_window.Camera, GameContext.WorldCamera.Current);
		Assert.Equal(_window.Camera, GameContext.WorldCamera.Previous);
	}

	[Fact]
	public void WorldCameraInterpolatesBetweenPushes()
	{
		var from = new Camera2D(new Vector2(0, 0), 1f);
		var to = new Camera2D(new Vector2(100, 0), 2f);

		GameContext.WorldCamera.Reset(from);
		GameContext.WorldCamera.Push(to);

		Assert.Equal(from, GameContext.WorldCamera.Previous);
		Assert.Equal(to, GameContext.WorldCamera.Current);

		var half = GameContext.WorldCamera.Evaluate(0.5f);
		Assert.Equal(new Vector2(50, 0), half.Position);
		Assert.Equal(1.5f, half.Zoom, 3);
	}
}
