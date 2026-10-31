using System;
using FixedMathSharp;
using FixedMathSharp.Geometry;

namespace EdgeOfPlain.Core.MathLib;

public static class Vector2Extension
{
	public static readonly Fixed64 Epsilon = (Fixed64)1e-6;

	extension(Vector2d vec2)
	{
		public static Fixed64 Cross(Vector2d a, Vector2d b) => a.X * b.Y - a.Y * b.X;
		public Vector2d Perpendicular() => new(-vec2.Y, vec2.X);

		public Vector2d RotateAround(Vector2d pivot, Fixed64 rad) => pivot + (vec2 - pivot).Rotate(rad);

		public Vector2d Project(Vector2d onto)
		{
			var denominator = Vector2d.Dot(onto, onto);
			return denominator > Epsilon * Epsilon
				? onto * (Vector2d.Dot(vec2, onto) / denominator)
				: Vector2d.Zero;
		}

		public Vector2d Reject(Vector2d onto) => vec2 - vec2.Project(onto);
		public Fixed64 Angle() => FixedMath.Atan2(vec2.Y, vec2.X);

		public static Fixed64 AngleBetween(Vector2d a, Vector2d b)
		{
			var lengthSquaredA = a.MagnitudeSquared;
			var lengthSquaredB = b.MagnitudeSquared;

			if (lengthSquaredA <= Epsilon * Epsilon || lengthSquaredB <= Epsilon * Epsilon)
				return Fixed64.Zero;

			var cosine = Vector2d.Dot(a, b) / FixedMath.Sqrt(lengthSquaredA * lengthSquaredB);
			return FixedMath.Acos(FixedMath.Clamp(cosine, -Fixed64.One, Fixed64.One));
		}

		public Vector2d NormalizedOrZero()
		{
			var lengthSquared = vec2.MagnitudeSquared;
			return lengthSquared > Epsilon * Epsilon
				? vec2.Normalized
				: Vector2d.Zero;
		}

		public Vector2d MoveToward(Vector2d target, Fixed64 maxDelta)
		{
			if (maxDelta < Fixed64.Zero)
				throw new ArgumentOutOfRangeException(nameof(maxDelta));

			var d = target - vec2;
			var len = d.Magnitude;
			return len <= maxDelta || len < Epsilon ? target : vec2 + d * (maxDelta / len);
		}

		public static bool Approximately(Vector2d a, Vector2d b, Fixed64 eps)
			=> FixedMath.Abs(a.X - b.X) < eps && FixedMath.Abs(a.Y - b.Y) < eps;

		public static bool Approximately(Vector2d a, Vector2d b)
			=> Approximately(a, b, Epsilon);
	}
}
