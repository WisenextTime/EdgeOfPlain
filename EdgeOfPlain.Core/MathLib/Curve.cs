using System;
using System.Numerics;

namespace EdgeOfPlain.Core.MathLib;

public static class Curve
{
	public static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
	{
		var t2 = t * t;
		var t3 = t2 * t;

		return 0.5f * (
			2f * p1 +
			(-p0 + p2) * t +
			(2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
			(-p0 + 3f * p1 - 3f * p2 + p3) * t3);
	}

	public static Vector2 Bezier(float t, params Vector2[] points)
	{
		Validate(points);

		var work = (Vector2[])points.Clone();

		for (var level = 1; level < work.Length; level++)
		{
			for (var i = 0; i < work.Length - level; i++)
				work[i] = Vector2.Lerp(work[i], work[i + 1], t);
		}

		return work[0];
	}

	public static Vector2 BezierTangent(float t, params Vector2[] points)
	{
		Validate(points);

		if (points.Length == 1) return Vector2.Zero;

		var degree = points.Length - 1;
		var derivative = new Vector2[degree];

		for (var i = 0; i < degree; i++)
			derivative[i] = (points[i + 1] - points[i]) * degree;

		return Bezier(t, derivative);
	}

	private static void Validate(Vector2[] points)
	{
		ArgumentNullException.ThrowIfNull(points);

		if (points.Length == 0)
			throw new ArgumentException("A curve needs at least one control point.", nameof(points));
	}
}
