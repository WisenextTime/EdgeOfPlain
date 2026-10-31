using System;
using FixedMathSharp;

namespace EdgeOfPlain.Core.MathLib;

public static class AngleExtension
{
	public static Fixed64 Normalize(Fixed64 rad)
	{
		var tau = Fixed64.TwoPi;
		var t = FixedMath.Floor((rad + Fixed64.Pi) / tau);
		return rad - tau * t;
	}

	public static Fixed64 NormalizeDeg(Fixed64 deg)
	{
		var t = FixedMath.Floor((deg + (Fixed64)180) / (Fixed64)360);
		return deg - (Fixed64)360 * t;
	}

	public static Fixed64 ShortestDelta(Fixed64 from, Fixed64 to) => Normalize(to - from);
	public static Fixed64 LerpAngle(Fixed64 from, Fixed64 to, Fixed64 t) => Normalize(from + ShortestDelta(from, to) * t);

	public static Fixed64 MoveTowardsAngle(Fixed64 cur, Fixed64 target, Fixed64 maxDelta)
	{
		if (maxDelta < Fixed64.Zero)
			throw new ArgumentOutOfRangeException(nameof(maxDelta));

		var d = ShortestDelta(cur, target);
		return FixedMath.Abs(d) <= maxDelta
			? target
			: Normalize(cur + (d < Fixed64.Zero ? -maxDelta : maxDelta));
	}
}
