using System.Diagnostics;
using FixedMathSharp;

namespace EdgeOfPlain.Core.Framework.Time;

public interface ITimeSource
{
	Fixed64 ElapsedSeconds { get; }
}

public sealed class StopwatchTimeSource : ITimeSource
{
	private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

	public Fixed64 ElapsedSeconds => (Fixed64)_stopwatch.Elapsed.TotalSeconds;
}
