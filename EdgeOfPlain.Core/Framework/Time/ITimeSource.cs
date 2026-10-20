using System.Diagnostics;

namespace EdgeOfPlain.Core.Framework.Time;

public interface ITimeSource
{
	double ElapsedSeconds { get; }
}

public sealed class StopwatchTimeSource : ITimeSource
{
	private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

	public double ElapsedSeconds => _stopwatch.Elapsed.TotalSeconds;
}