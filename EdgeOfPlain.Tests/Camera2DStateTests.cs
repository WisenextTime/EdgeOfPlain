using System.Numerics;
using EdgeOfPlain.Core.Framework.Graphics;
using Xunit;

namespace EdgeOfPlain.Tests;

public sealed class Camera2DStateTests
{
	[Fact]
	public void ConstructorSeedsPreviousAndCurrent()
	{
		var camera = new Camera2D(new Vector2(10, 20), 3f);
		var state = new Camera2DState(camera);

		Assert.Equal(camera, state.Previous);
		Assert.Equal(camera, state.Current);
	}

	[Fact]
	public void PushShiftsPreviousToCurrent()
	{
		var first = new Camera2D(new Vector2(10, 20), 1f);
		var second = new Camera2D(new Vector2(30, 40), 2f);
		var state = new Camera2DState(first);

		state.Push(second);

		Assert.Equal(first, state.Previous);
		Assert.Equal(second, state.Current);
	}

	[Fact]
	public void ResetSetsBothSnapshots()
	{
		var camera = new Camera2D(new Vector2(10, 20), 3f);
		var state = new Camera2DState(Camera2D.Default);

		state.Reset(camera);

		Assert.Equal(camera, state.Previous);
		Assert.Equal(camera, state.Current);
	}

	[Fact]
	public void EvaluateInterpolatesPreviousAndCurrent()
	{
		var from = new Camera2D(new Vector2(0, 0), 1f);
		var to = new Camera2D(new Vector2(100, 100), 3f);
		var state = new Camera2DState(from);

		state.Push(to);
		var result = state.Evaluate(0.25f);

		Assert.Equal(new Vector2(25, 25), result.Position);
		Assert.Equal(1.5f, result.Zoom, 3);
	}

	[Fact]
	public void SetAppliesImmediatelyWithoutInterpolation()
	{
		var from = new Camera2D(new Vector2(0, 0), 1f);
		var zoomed = new Camera2D(new Vector2(0, 0), 2f);
		var state = new Camera2DState(from);

		state.Push(zoomed);
		state.Set(zoomed);

		Assert.Equal(zoomed, state.Previous);
		Assert.Equal(zoomed, state.Current);
		Assert.Equal(zoomed, state.Evaluate(0f));
		Assert.Equal(zoomed, state.Evaluate(0.37f));
		Assert.Equal(zoomed, state.Evaluate(1f));
	}

	[Fact]
	public void PushingCurrentAgainCommitsInterpolation()
	{
		var from = new Camera2D(new Vector2(0, 0), 1f);
		var to = new Camera2D(new Vector2(10, 0), 2f);
		var state = new Camera2DState(from);

		state.Push(to);
		state.Push(to);

		Assert.Equal(to, state.Previous);
		Assert.Equal(to, state.Current);
	}

	[Fact]
	public void EvaluateClampsAlpha()
	{
		var from = new Camera2D(new Vector2(0, 0), 1f);
		var to = new Camera2D(new Vector2(100, 0), 1f);
		var state = new Camera2DState(from);

		state.Push(to);

		Assert.Equal(from, state.Evaluate(-2f));
		Assert.Equal(to, state.Evaluate(3f));
	}
}
