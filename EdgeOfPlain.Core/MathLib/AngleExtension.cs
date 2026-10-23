using System;

namespace EdgeOfPlain.Core.MathLib;

public static class AngleExtension
{
	public static float Normalize(float rad) => MathF.IEEERemainder(rad, MathF.Tau);
	public static float NormalizeDeg(float deg) => MathF.IEEERemainder(deg, 360f);
	public static float ShortestDelta(float from, float to) => Normalize(to - from);
	public static float LerpAngle(float from, float to, float t) => Normalize(from + ShortestDelta(from, to) * t);

	public static float MoveTowardsAngle(float cur, float target, float maxDelta)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(maxDelta);

		var d = ShortestDelta(cur, target);
		return MathF.Abs(d) <= maxDelta ? target : Normalize(cur + MathF.Sign(d) * maxDelta);
	}
}