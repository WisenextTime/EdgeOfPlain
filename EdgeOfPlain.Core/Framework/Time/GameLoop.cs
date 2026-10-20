using System;

namespace EdgeOfPlain.Core.Framework.Time;

public readonly record struct LogicTime(long Tick, double Delta, double TotalSeconds);

public readonly record struct RenderTime(long Tick, float Alpha, double FrameDelta, double TotalSeconds);

public sealed class GameLoop
{
	public const double DefaultFixedStep = 1.0 / 60.0;

	public const double MaxFrameDelta = 0.25;

	private readonly ITimeSource _time;
	private readonly int _maxStepsPerFrame;
	private double _lastSeconds;
	private double _accumulator;
	private long _tick;
	private int _pendingSteps;
	private double _frameDelta;

	public GameLoop(ITimeSource time, double fixedStep = DefaultFixedStep, int maxStepsPerFrame = 5)
	{
		ArgumentNullException.ThrowIfNull(time);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fixedStep);
		ArgumentOutOfRangeException.ThrowIfLessThan(maxStepsPerFrame, 1);

		_time = time;
		FixedStep = fixedStep;
		_maxStepsPerFrame = maxStepsPerFrame;
		_lastSeconds = time.ElapsedSeconds;
	}

	public double FixedStep { get; }

	public long Tick => _tick;

	public int PendingSteps => _pendingSteps;

	public double TotalSeconds => _tick * FixedStep;

	public float Alpha => (float)Math.Clamp(_accumulator / FixedStep, 0.0, 1.0);

	public RenderTime RenderTime => new(_tick, Alpha, _frameDelta, TotalSeconds);

	public void BeginFrame()
	{
		var now = _time.ElapsedSeconds;
		var elapsed = now - _lastSeconds;
		_lastSeconds = now;

		if (elapsed < 0) elapsed = 0;
		if (elapsed > MaxFrameDelta) elapsed = MaxFrameDelta;
		_frameDelta = elapsed;

		_accumulator += elapsed;

		var steps = (int)(_accumulator / FixedStep);
		if (steps > _maxStepsPerFrame)
		{
			steps = _maxStepsPerFrame;
			_accumulator = steps * FixedStep;
		}

		_pendingSteps = steps;
	}

	public bool TryStep(out LogicTime time)
	{
		if (_pendingSteps <= 0)
		{
			time = default;
			return false;
		}

		_pendingSteps--;
		_accumulator -= FixedStep;
		_tick++;

		time = new LogicTime(_tick, FixedStep, TotalSeconds);
		return true;
	}
}