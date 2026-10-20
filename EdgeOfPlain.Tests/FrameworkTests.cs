using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using EdgeOfPlain.Core.Framework.Events;
using EdgeOfPlain.Core.Framework.Graphics;
using EdgeOfPlain.Core.Framework.Input;
using EdgeOfPlain.Core.Framework.Scenes;
using EdgeOfPlain.Core.Framework.Time;
using Xunit;

namespace EdgeOfPlain.Tests;

internal sealed class FakeTimeSource : ITimeSource
{
	public double ElapsedSeconds { get; set; }
}

public sealed class GameLoopTests
{
	private const double Step = GameLoop.DefaultFixedStep;

	[Fact]
	public void ProducesNoStepBeforeOneFixedStepElapsed()
	{
		var time = new FakeTimeSource();
		var loop = new GameLoop(time);

		time.ElapsedSeconds = Step / 2;
		loop.BeginFrame();

		Assert.False(loop.TryStep(out _));
		Assert.Equal(0, loop.Tick);
		Assert.Equal(0.5f, loop.Alpha, 5);
	}

	[Fact]
	public void ProducesOneStepPerFixedStep()
	{
		var time = new FakeTimeSource();
		var loop = new GameLoop(time);

		time.ElapsedSeconds = Step * 2;
		loop.BeginFrame();

		Assert.True(loop.TryStep(out var first));
		Assert.True(loop.TryStep(out var second));
		Assert.False(loop.TryStep(out _));
		Assert.Equal(1, first.Tick);
		Assert.Equal(2, second.Tick);
		Assert.Equal(2 * Step, loop.TotalSeconds, 9);
	}

	[Fact]
	public void ClampsVeryLongFrames()
	{
		var time = new FakeTimeSource();
		var loop = new GameLoop(time);

		time.ElapsedSeconds = 10;
		loop.BeginFrame();

		Assert.Equal(5, loop.PendingSteps);
		Assert.InRange(loop.RenderTime.FrameDelta, 0, GameLoop.MaxFrameDelta);
	}

	[Fact]
	public void IgnoresNegativeTimeJumps()
	{
		var time = new FakeTimeSource { ElapsedSeconds = 5 };
		var loop = new GameLoop(time);

		time.ElapsedSeconds = 4;
		loop.BeginFrame();

		Assert.Equal(0, loop.PendingSteps);
		Assert.Equal(0d, loop.RenderTime.FrameDelta);
	}

	[Fact]
	public void AlphaIsFractionOfRemainingAccumulator()
	{
		var time = new FakeTimeSource();
		var loop = new GameLoop(time);

		time.ElapsedSeconds = Step * 1.5;
		loop.BeginFrame();
		Assert.True(loop.TryStep(out _));

		Assert.Equal(0.5f, loop.Alpha, 5);
	}

	[Fact]
	public void RejectsInvalidSettings()
	{
		Assert.Throws<ArgumentNullException>(() => new GameLoop(null!));
		Assert.Throws<ArgumentOutOfRangeException>(() => new GameLoop(new FakeTimeSource(), 0));
		Assert.Throws<ArgumentOutOfRangeException>(() => new GameLoop(new FakeTimeSource(), Step, 0));
	}
}

public sealed class Camera2DTests
{
	private static readonly Vector2 _pixels = new(1280, 720);

	[Fact]
	public void DefaultCameraMapsWorldOriginToTopLeftInPixelSpace()
	{
		var camera = Camera2D.Default;

		Assert.Equal(new Vector2(640, 360), camera.WorldToScreen(Vector2.Zero, _pixels));
	}

	[Fact]
	public void WorldAndScreenConversionRoundTrip()
	{
		var camera = new Camera2D(new Vector2(120, -40), 2.5f, 0.3f);
		var world = new Vector2(17, 23);

		var screen = camera.WorldToScreen(world, _pixels);
		var restored = camera.ScreenToWorld(screen, _pixels);

		Assert.Equal(world.X, restored.X, 3);
		Assert.Equal(world.Y, restored.Y, 3);
	}

	[Fact]
	public void WorldSizeScalesWithZoom()
	{
		var camera = new Camera2D(Vector2.Zero, 2f);

		Assert.Equal(new Vector2(640, 360), camera.WorldSize(_pixels));
	}

	[Fact]
	public void CullingBounds_MatchesViewWhenUnrotated()
	{
		var camera = new Camera2D(new Vector2(100, 50), 2f);

		var bounds = camera.CullingBounds(_pixels);

		Assert.Equal(-220f, bounds.Left, 3);
		Assert.Equal(-130f, bounds.Top, 3);
		Assert.Equal(640f, bounds.Width, 3);
		Assert.Equal(360f, bounds.Height, 3);
	}

	[Fact]
	public void CullingBounds_ExpandsToContainRotatedView()
	{
		var camera = new Camera2D(Vector2.Zero, 1f, MathF.PI / 4f);

		var bounds = camera.CullingBounds(_pixels);

		Assert.Equal(1414.2136f, bounds.Width, 1);
		Assert.Equal(1414.2136f, bounds.Height, 1);
	}

	[Fact]
	public void CullingBounds_CoversEveryRotatedViewCorner()
	{
		var camera = new Camera2D(new Vector2(10, -20), 1.5f, 0.7f);
		var half = camera.WorldSize(_pixels) / 2f;
		var cos = MathF.Cos(camera.Rotation);
		var sin = MathF.Sin(camera.Rotation);

		var bounds = camera.CullingBounds(_pixels);

		foreach (var corner in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) })
		{
			var local = corner * half;
			var world = camera.Position + new Vector2(local.X * cos - local.Y * sin, local.X * sin + local.Y * cos);

			Assert.InRange(world.X, bounds.Left, bounds.Right);
			Assert.InRange(world.Y, bounds.Top, bounds.Bottom);
		}
	}

	[Fact]
	public void NonPositiveZoomIsRejected()
	{
		var camera = new Camera2D(Vector2.Zero, 0f);

		Assert.Throws<ArgumentOutOfRangeException>(() => camera.WorldSize(_pixels));
		Assert.Throws<ArgumentOutOfRangeException>(() => new Camera2D(Vector2.Zero, float.NaN).WorldToScreen(default, _pixels));
	}

	[Fact]
	public void ClampTo_KeepsCameraInsideWorld()
	{
		var bounds = new RectangleF(0, 0, 5000, 4000);
		var camera = new Camera2D(new Vector2(-100, -100));

		var clamped = camera.ClampTo(bounds, _pixels);

		Assert.Equal(640, clamped.Position.X);
		Assert.Equal(360, clamped.Position.Y);
	}

	[Fact]
	public void ClampTo_CentersSmallWorld()
	{
		var bounds = new RectangleF(0, 0, 100, 100);

		var clamped = Camera2D.Default.ClampTo(bounds, _pixels);

		Assert.Equal(50, clamped.Position.X);
		Assert.Equal(50, clamped.Position.Y);
	}

	[Fact]
	public void NormalizeAngle_WrapsIntoHalfTurnRange()
	{
		Assert.Equal(0, Camera2D.NormalizeAngle(MathF.Tau * 2), 5);
		Assert.Equal(MathF.PI, MathF.Abs(Camera2D.NormalizeAngle(-MathF.PI)), 5);
		Assert.Equal(0.25f, Camera2D.NormalizeAngle(MathF.Tau + 0.25f), 5);
	}

	[Fact]
	public void Interpolate_TakesShortestRotationPath()
	{
		var from = new Camera2D(Vector2.Zero, 1f, 3.0f);
		var to = new Camera2D(Vector2.Zero, 1f, -3.0f);

		var half = Camera2D.Interpolate(from, to, 0.5f);

		Assert.InRange(MathF.Abs(Camera2D.NormalizeAngle(half.Rotation)), 3.1415f, 3.1416f);
	}
}

public sealed class EventBusTests
{
	[Fact]
	public void Publish_InvokesSubscribersInOrder()
	{
		EventBus.Clear();
		var calls = new List<int>();

		using var first = EventBus.Subscribe<int>(calls.Add);
		using var second = EventBus.Subscribe<int>(value => calls.Add(value * 10));

		EventBus.Publish(1);

		Assert.Equal([1, 10], calls);
	}

	[Fact]
	public void DisposingSubscriptionStopsDelivery()
	{
		EventBus.Clear();
		var calls = 0;

		var subscription = EventBus.Subscribe<string>(_ => calls++);
		EventBus.Publish("a");
		subscription.Dispose();
		EventBus.Publish("a");

		Assert.Equal(1, calls);
		Assert.Equal(0, EventBus.SubscriberCount<string>());
		Assert.False(EventBus.HasSubscribers<string>());
	}

	[Fact]
	public void DisposingOneOfManyKeepsTheRest()
	{
		EventBus.Clear();
		var calls = 0;

		var a = EventBus.Subscribe((Action<int>)First);
		using var b = EventBus.Subscribe((Action<int>)Second);
		using var c = EventBus.Subscribe((Action<int>)Third);

		a.Dispose();
		EventBus.Publish(0);
		a.Dispose();

		Assert.Equal(110, calls);
		Assert.Equal(2, EventBus.SubscriberCount<int>());
		return;

		void First(int _)
		{
			calls++;
		}

		void Second(int _)
		{
			calls += 10;
		}

		void Third(int _)
		{
			calls += 100;
		}
	}

	[Fact]
	public void PublishWithoutSubscribers_IsNoOp()
	{
		EventBus.Clear();

		EventBus.Publish(new WindowResizedEvent(new Size(1, 1), new Size(2, 2)));
	}

	[Fact]
	public void Publish_ReentrantSubscriptionDoesNotAffectCurrentDispatch()
	{
		EventBus.Clear();
		var calls = 0;

		using var subscription = EventBus.Subscribe<int>(_ =>
		{
			calls++;
			EventBus.Subscribe<int>(_ => calls += 100);
		});

		EventBus.Publish(0);

		Assert.Equal(1, calls);
	}

	[Fact]
	public void HandlerFailureDoesNotSkipRemainingSubscribers()
	{
		EventBus.Clear();
		var calls = 0;

		using var failing = EventBus.Subscribe<int>(_ =>
		{
			calls++;
			throw new InvalidOperationException("boom");
		});
		using var healthy = EventBus.Subscribe<int>(_ => calls += 10);

		Assert.Throws<AggregateException>(() => EventBus.Publish(0));
		Assert.Equal(11, calls);
	}

	[Fact]
	public void SubscriptionCopy_CanBeDisposedTwice()
	{
		EventBus.Clear();
		var calls = 0;
		var subscription = EventBus.Subscribe<int>(_ => calls++);
		var copy = subscription;

		copy.Dispose();
		subscription.Dispose();

		Assert.True(subscription.IsActive);
		EventBus.Publish(0);
		Assert.Equal(0, calls);
	}
}

internal sealed class FakeWindow : IGameWindow
{
	public bool IsOpen { get; set; } = true;

	public Size Size { get; set; } = new(1280, 720);

	public Rectangle Viewport { get; set; }

	public Camera2D Camera { get; set; } = Camera2D.Default;

	public int ClearCount { get; private set; }

	public int PresentCount { get; private set; }

	public void ResetCamera() => Camera = Camera2D.Default;

	public void PollEvents() { }

	public void WaitForEvents(TimeSpan timeout) { }

	public void ActivateContext() { }

	public void DeactivateContext() { }

	public void Clear(ColorF color) => ClearCount++;

	public void Present() => PresentCount++;
}

internal sealed class RecordingScene(string name) : Scene
{
	public List<string> Log { get; } = [];

	public bool HandleAll { get; init; }

	public override string Name => name;

	public override void Enter(SceneContext context) => Log.Add($"enter:{name}");

	public override void Exit() => Log.Add($"exit:{name}");

	public override void Update(LogicTime time, IInputState input) => Log.Add($"update:{name}");

	public override bool HandleInput(IInputState input)
	{
		Log.Add($"input:{name}");
		return HandleAll;
	}

	public override void Draw(RenderTime time) => Log.Add($"draw:{name}");
}

public sealed class SceneManagerTests
{
	private static (SceneManager Scenes, FakeWindow Window, List<string> Log) Create()
	{
		EventBus.Clear();
		var scenes = new SceneManager();
		var window = new FakeWindow();
		scenes.Attach(window);
		return (scenes, window, []);
	}

	[Fact]
	public void PushIsDeferredUntilPump()
	{
		var (scenes, _, _) = Create();
		var scene = new RecordingScene("a");

		scenes.Push(scene);

		Assert.Null(scenes.Active);
		scenes.Update(default, new InputState());
		Assert.Same(scene, scenes.Active);
		Assert.Equal(new[] { "enter:a" }, scene.Log);
	}

	[Fact]
	public void PopExitsTopScene()
	{
		var (scenes, _, _) = Create();
		var first = new RecordingScene("a");
		var second = new RecordingScene("b");
		scenes.Push(first);
		scenes.Push(second);
		scenes.Update(default, new InputState());

		scenes.Pop();
		scenes.Update(default, new InputState());

		Assert.Same(first, scenes.Active);
		Assert.Equal(new[] { "enter:a", "update:a" }, first.Log);
		Assert.Equal(new[] { "enter:b", "update:b", "exit:b" }, second.Log);
	}

	[Fact]
	public void PopOnEmptyStackIsIgnored()
	{
		var (scenes, _, _) = Create();

		scenes.Pop();
		scenes.Update(default, new InputState());

		Assert.Null(scenes.Active);
	}

	[Fact]
	public void ReplaceSwapsTopScene()
	{
		var (scenes, _, _) = Create();
		var first = new RecordingScene("a");
		var replacement = new RecordingScene("c");
		scenes.Push(first);
		scenes.Update(default, new InputState());

		scenes.Replace(replacement);
		scenes.Update(default, new InputState());

		Assert.Same(replacement, scenes.Active);
		Assert.Equal(new[] { "enter:a", "update:a", "exit:a" }, first.Log);
		Assert.Equal(new[] { "enter:c" }, replacement.Log);
	}

	[Fact]
	public void PopToUnwindsEverythingAboveTarget()
	{
		var (scenes, _, _) = Create();
		var root = new RecordingScene("root");
		var middle = new RecordingScene("middle");
		var top = new RecordingScene("top");
		scenes.Push(root);
		scenes.Push(middle);
		scenes.Push(top);
		scenes.Update(default, new InputState());

		scenes.PopTo(root);
		scenes.Update(default, new InputState());

		Assert.Same(root, scenes.Active);
		Assert.Contains("exit:top", top.Log);
		Assert.Contains("exit:middle", middle.Log);
	}

	[Fact]
	public void PopToUnknownSceneIsIgnored()
	{
		var (scenes, _, _) = Create();
		var root = new RecordingScene("root");
		scenes.Push(root);
		scenes.Update(default, new InputState());

		scenes.PopTo(new RecordingScene("stranger"));
		scenes.Update(default, new InputState());

		Assert.Same(root, scenes.Active);
	}

	[Fact]
	public void ClearRemovesAllScenes()
	{
		var (scenes, _, _) = Create();
		scenes.Push(new RecordingScene("a"));
		scenes.Push(new RecordingScene("b"));
		scenes.Update(default, new InputState());

		scenes.Clear();
		scenes.Update(default, new InputState());

		Assert.Null(scenes.Active);
		Assert.Empty(scenes.Scenes);
	}

	[Fact]
	public void InputStopsAtFirstHandlingScene()
	{
		var (scenes, _, _) = Create();
		var bottom = new RecordingScene("bottom");
		var top = new RecordingScene("top") { HandleAll = true };
		scenes.Push(bottom);
		scenes.Push(top);
		scenes.Update(default, new InputState());

		scenes.HandleInput(new InputState());

		Assert.Equal(new[] { "input:top" }, top.Log.Where(line => line.StartsWith("input:")).ToArray());
		Assert.DoesNotContain("input:bottom", bottom.Log);
	}

	[Fact]
	public void DisabledOrHiddenScenesAreSkipped()
	{
		var (scenes, _, _) = Create();
		var scene = new RecordingScene("a") { IsEnabled = false, IsVisible = false };
		scenes.Push(scene);
		scenes.Update(default, new InputState());

		scenes.Update(default, new InputState());
		scenes.Draw(default);

		Assert.DoesNotContain("update:a", scene.Log);
		Assert.DoesNotContain("draw:a", scene.Log);
	}

	[Fact]
	public void SceneChangeEventsArePublished()
	{
		var (scenes, _, _) = Create();
		var kinds = new List<SceneChangeKind>();
		using var subscription = EventBus.Subscribe<SceneChangedEvent>(e => kinds.Add(e.Kind));

		scenes.Push(new RecordingScene("a"));
		scenes.Update(default, new InputState());
		scenes.Pop();
		scenes.Update(default, new InputState());

		Assert.Equal(new[] { SceneChangeKind.Push, SceneChangeKind.Pop }, kinds);
	}

	[Fact]
	public void ContextRequiresAttachedWindow()
	{
		var scenes = new SceneManager();

		Assert.Throws<InvalidOperationException>(() => scenes.Context);
	}

	private static List<string> Combine(RecordingScene first, RecordingScene second)
	{
		var result = new List<string>(first.Log);
		foreach (var line in second.Log)
			if (!result.Contains(line))
				result.Add(line);
		return result;
	}

	private sealed class NullPipeline : IRenderPipeline
	{
		public void Begin() { }

		public void End() { }
	}
}

public sealed class InputStateTests
{
	[Fact]
	public void KeyDown_RaisesPressedUntilNextFrame()
	{
		var input = new InputState();

		input.OnKeyDown(Key.A);

		Assert.True(input.IsKeyDown(Key.A));
		Assert.True(input.WasKeyPressed(Key.A));
		Assert.False(input.WasKeyReleased(Key.A));

		input.BeginFrame();

		Assert.True(input.IsKeyDown(Key.A));
		Assert.False(input.WasKeyPressed(Key.A));
	}

	[Fact]
	public void RepeatedKeyDown_DoesNotRaisePressedTwice()
	{
		var input = new InputState();
		input.OnKeyDown(Key.A);

		input.OnKeyDown(Key.A);

		Assert.True(input.WasKeyPressed(Key.A));
		Assert.True(input.IsKeyDown(Key.A));
	}

	[Fact]
	public void KeyUp_RaisesReleasedOnlyWhenHeld()
	{
		var input = new InputState();

		input.OnKeyUp(Key.B);

		Assert.False(input.WasKeyReleased(Key.B));

		input.OnKeyDown(Key.B);
		input.BeginFrame();
		input.OnKeyUp(Key.B);

		Assert.False(input.IsKeyDown(Key.B));
		Assert.True(input.WasKeyReleased(Key.B));
	}

	[Fact]
	public void MouseButtonsTrackPressedAndReleased()
	{
		var input = new InputState();

		input.OnMouseButtonDown(MouseButton.Right);

		Assert.True(input.IsMouseButtonDown(MouseButton.Right));
		Assert.True(input.WasMouseButtonPressed(MouseButton.Right));

		input.BeginFrame();
		input.OnMouseButtonUp(MouseButton.Right);

		Assert.False(input.IsMouseButtonDown(MouseButton.Right));
		Assert.True(input.WasMouseButtonReleased(MouseButton.Right));
	}

	[Fact]
	public void MouseDeltaAccumulatesWithinFrameAndResetsAfterward()
	{
		var input = new InputState();

		input.OnMouseMoved(10, 20);
		input.BeginFrame();
		input.OnMouseMoved(14, 26);

		Assert.Equal(new Vector2(14, 26), input.MousePosition);
		Assert.Equal(new Vector2(4, 6), input.MouseDelta);

		input.BeginFrame();

		Assert.Equal(Vector2.Zero, input.MouseDelta);
	}

	[Fact]
	public void FirstMouseMoveDoesNotProduceDelta()
	{
		var input = new InputState();

		input.OnMouseMoved(400, 300);

		Assert.Equal(new Vector2(400, 300), input.MousePosition);
		Assert.Equal(Vector2.Zero, input.MouseDelta);

		input.OnMouseMoved(410, 300);

		Assert.Equal(new Vector2(10, 0), input.MouseDelta);

		input.Reset();
		input.OnMouseMoved(900, 900);

		Assert.Equal(Vector2.Zero, input.MouseDelta);
	}

	[Fact]
	public void ScrollAccumulatesWithinFrameAndResetsAfterward()
	{
		var input = new InputState();

		input.OnScroll(1.5f);
		input.OnScroll(-0.5f);

		Assert.Equal(1f, input.ScrollDelta, 5);

		input.BeginFrame();

		Assert.Equal(0f, input.ScrollDelta);
	}

	[Fact]
	public void PostedEventsOnlyApplyWhenPendingIsDrained()
	{
		var input = new InputState();

		input.PostKeyDown(Key.C);

		Assert.False(input.IsKeyDown(Key.C));

		input.ApplyPending();

		Assert.True(input.IsKeyDown(Key.C));
	}

	[Fact]
	public void ResetClearsHeldState()
	{
		var input = new InputState();
		input.OnKeyDown(Key.D);
		input.OnMouseMoved(5, 5);
		input.OnScroll(2f);

		input.Reset();

		Assert.False(input.IsKeyDown(Key.D));
		Assert.Equal(Vector2.Zero, input.MouseDelta);
		Assert.Equal(0f, input.ScrollDelta);
	}

	[Fact]
	public void UnknownKeyNeverThrows()
	{
		var input = new InputState();

		Assert.False(input.IsKeyDown(Key.Unknown));
		Assert.False(input.WasKeyPressed((Key)12345));
	}
}