using System;
using FixedMathSharp;
using FixedMathSharp.Geometry;

namespace EdgeOfPlain.Core.MathLib;

public static class Curve
{
	public static Vector2d CatmullRom(Vector2d p0, Vector2d p1, Vector2d p2, Vector2d p3, Fixed64 t)
	{
		var t2 = t * t;
		var t3 = t2 * t;

		return Fixed64.Half * (
			(Fixed64)2 * p1 +
			(-p0 + p2) * t +
			((Fixed64)2 * p0 - (Fixed64)5 * p1 + (Fixed64)4 * p2 - p3) * t2 +
			(-p0 + (Fixed64)3 * p1 - (Fixed64)3 * p2 + p3) * t3);
	}

	public static Vector2d Bezier(Fixed64 t, params Vector2d[] points)
	{
		Validate(points);

		var work = (Vector2d[])points.Clone();

		for (var level = 1; level < work.Length; level++)
		{
			for (var i = 0; i < work.Length - level; i++)
				work[i] = Vector2d.Lerp(work[i], work[i + 1], t);
		}

		return work[0];
	}

	public static Vector2d BezierTangent(Fixed64 t, params Vector2d[] points)
	{
		Validate(points);

		if (points.Length == 1) return Vector2d.Zero;

		var degree = points.Length - 1;
		var derivative = new Vector2d[degree];

		for (var i = 0; i < degree; i++)
			derivative[i] = (points[i + 1] - points[i]) * (Fixed64)degree;

		return Bezier(t, derivative);
	}

	private static void Validate(Vector2d[] points)
	{
		ArgumentNullException.ThrowIfNull(points);

		if (points.Length == 0)
			throw new ArgumentException("A curve needs at least one control point.", nameof(points));
	}
}
