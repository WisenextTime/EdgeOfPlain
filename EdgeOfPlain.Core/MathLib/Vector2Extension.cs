using System;
using System.Numerics;

namespace EdgeOfPlain.Core.MathLib;

public static class Vector2Extension
{
	public const float Epsilon = 1e-6f;
	extension(Vector2 vec2)
	{
		public static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
		public Vector2 Perpendicular() => new(-vec2.Y, vec2.X);

		public Vector2 Rotate(float rad)
		{
			var (s, c) = MathF.SinCos(rad);
			return new Vector2(vec2.X * c - vec2.Y * s, vec2.X * s + vec2.Y * c);
		}

		public Vector2 RotateAround(Vector2 pivot, float rad) => pivot + (vec2 - pivot).Rotate(rad);
		public Vector2 Project(Vector2 onto)
		{
			var denominator = Vector2.Dot(onto, onto);
			return denominator > Epsilon * Epsilon
				? onto * (Vector2.Dot(vec2, onto) / denominator)
				: Vector2.Zero;
		}

		public Vector2 Reject(Vector2 onto) => vec2 - vec2.Project(onto);
		public float Angle() => MathF.Atan2(vec2.Y, vec2.X);

		public static float AngleBetween(Vector2 a, Vector2 b)
		{
			var lengthSquaredA = a.LengthSquared();
			var lengthSquaredB = b.LengthSquared();

			if (lengthSquaredA <= Epsilon * Epsilon || lengthSquaredB <= Epsilon * Epsilon)
				return 0f;

			var cosine = Vector2.Dot(a, b) / MathF.Sqrt(lengthSquaredA * lengthSquaredB);
			return MathF.Acos(Math.Clamp(cosine, -1f, 1f));
		}

		public Vector2 NormalizedOrZero()
		{
			var lengthSquared = vec2.LengthSquared();
			return lengthSquared > Epsilon * Epsilon
				? vec2 / MathF.Sqrt(lengthSquared)
				: Vector2.Zero;
		}

		public Vector2 MoveToward(Vector2 target, float maxDelta)
		{
			ArgumentOutOfRangeException.ThrowIfNegative(maxDelta);

			var d = target - vec2;
			var len = d.Length();
			return len <= maxDelta || len < Epsilon ? target : vec2 + d * (maxDelta / len);
		}
		
		public static bool Approximately(Vector2 a, Vector2 b, float eps = Epsilon)
			=> MathF.Abs(a.X -b.X) < eps && MathF.Abs(a.Y - b.Y) < eps;
	}
}