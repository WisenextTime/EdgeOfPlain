using System;
using FixedMathSharp;

namespace EdgeOfPlain.Core.Framework.Time;

public readonly record struct LogicTime(long Tick, Fixed64 Delta, Fixed64 TotalSeconds);

public readonly record struct RenderTime(long Tick, Fixed64 Alpha, Fixed64 FrameDelta, Fixed64 TotalSeconds);

public sealed class GameLoop
{
	public static readonly Fixed64 DefaultFixedStep = (Fixed64)0.1;

	public static readonly Fixed64 MaxFrameDelta = (Fixed64)0.25;

	private readonly ITimeSource _time;
	private readonly int _maxStepsPerFrame;
	private Fixed64 _lastSeconds;
	private Fixed64 _accumulator;
	private long _tick;
	private Fixed64 _frameDelta;

	public GameLoop(ITimeSource time) : this(time, DefaultFixedStep) { }

	public GameLoop(ITimeSource time, Fixed64 fixedStep, int maxStepsPerFrame = 5)
	{
		ArgumentNullException.ThrowIfNull(time);

		if (fixedStep <= Fixed64.Zero)
			throw new ArgumentOutOfRangeException(nameof(fixedStep), fixedStep, "Fixed step must be positive.");

		ArgumentOutOfRangeException.ThrowIfLessThan(maxStepsPerFrame, 1);

		_time = time;
		FixedStep = fixedStep;
		_maxStepsPerFrame = maxStepsPerFrame;
		_lastSeconds = _time.ElapsedSeconds;
	}

	public Fixed64 FixedStep { get; }

	public long Tick => _tick;

	public int PendingSteps { get; private set; }

	public Fixed64 TotalSeconds => (Fixed64)_tick * FixedStep;

	public Fixed64 Alpha => FixedMath.Clamp(_accumulator / FixedStep, Fixed64.Zero, Fixed64.One);

	public RenderTime RenderTime => new(_tick, Alpha, _frameDelta, TotalSeconds);

	public void BeginFrame()
	{
		var now = _time.ElapsedSeconds;
		var elapsed = now - _lastSeconds;
		_lastSeconds = now;

		if (elapsed < Fixed64.Zero) elapsed = Fixed64.Zero;
		if (elapsed > MaxFrameDelta) elapsed = MaxFrameDelta;
		_frameDelta = elapsed;

		_accumulator += elapsed;

		var steps = (int)(_accumulator / FixedStep);
		if (steps > _maxStepsPerFrame)
		{
			steps = _maxStepsPerFrame;
			_accumulator = (Fixed64)steps * FixedStep;
		}

		PendingSteps = steps;
	}

	public bool TryStep(out LogicTime time)
	{
		if (PendingSteps <= 0)
		{
			time = default;
			return false;
		}

		PendingSteps--;
		_accumulator -= FixedStep;
		_tick++;

		time = new LogicTime(_tick, FixedStep, TotalSeconds);
		return true;
	}
}