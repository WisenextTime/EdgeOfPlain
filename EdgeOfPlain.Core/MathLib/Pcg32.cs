using System;
using System.Collections.Generic;
using System.Numerics;

namespace EdgeOfPlain.Core.MathLib;

public struct Pcg32
{
	private ulong _state;
	private ulong _inc;

	private const ulong Multiplier = 6364136223846793005ul;

	public Pcg32(ulong seed, ulong sequence = 54ul)
	{
		_state = 0ul;
		_inc = sequence << 1 | 1ul;
		NextUint();
		_state += seed;
		NextUint();
	}

	public uint NextUint()
	{
		var old = _state;
		_state = unchecked(old * Multiplier + _inc);
		var xorShifted = (uint)((old >> 18 ^ old) >> 27);
		var rot = (int)(old >> 59);
		return xorShifted >> rot | xorShifted << ((-rot) & 31);
	}

	public float NextFloat() => (NextUint() >> 8) * (1f / (1 << 24));

	public double NextDouble()
	{
		var high = NextUint() >> 5;
		var low = NextUint() >> 6;
		return (high * 67108864d + low) / 9007199254740992d;
	}

	public float RangeFloat(float min, float max) => min + NextFloat() * (max - min);
	public bool Change(float p) => p > NextFloat();

	public int Range(int min, int max)
	{
		if (max <= min) return min;

		var range = (ulong)((long)max - min);
		var r = NextUint() * range >> 32;
		return (int)(min + (long)r);
	}

	public float NextAngle() => NextFloat() * MathF.Tau;

	public Vector2 InsideUnitCircle()
	{
		var angle = NextAngle();
		var radius = MathF.Sqrt(NextFloat());
		var (s, c) = MathF.SinCos(angle);
		return new Vector2(c * radius, s * radius);
	}

	public Vector2 InsideCircle(Vector2 center, float radius) => center + radius * InsideUnitCircle();

	public Vector2 OnUnitCircle()
	{
		var angle = NextAngle();
		var (s, c) = MathF.SinCos(angle);
		return new Vector2(c, s);
	}

	public Vector2 OnCircle(Vector2 center, float radius) => center + radius * OnUnitCircle();
	public Vector2 InsideRect(Vector2 lt, Vector2 rd) => new Vector2(RangeFloat(lt.X, rd.X), RangeFloat(lt.Y, rd.Y));

	public float Gaussian(float mean = 0f, float stDev = 1f)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(stDev);

		var u1 = 1f - NextFloat();
		var u2 = NextFloat();
		var mag = MathF.Sqrt(-2f * MathF.Log(u1));
		return mean + stDev * mag * MathF.Cos(MathF.Tau * u2);
	}

	public void Shuffle<T>(IList<T> list)
	{
		ArgumentNullException.ThrowIfNull(list);

		for (var i = 0; i < list.Count; i++)
		{
			var j = Range(0, i + 1);
			(list[i], list[j]) = (list[j], list[i]);
		}
	}

	public int PickWeightedIndex(IEnumerable<float> weights)
	{
		ArgumentNullException.ThrowIfNull(weights);

		var list = weights as float[] ?? [.. weights];

		if (list.Length == 0)
			throw new ArgumentException("At least one weight is required.", nameof(weights));

		var total = 0f;
		foreach (var weight in list)
		{
			if (!float.IsFinite(weight) || weight < 0f)
				throw new ArgumentOutOfRangeException(nameof(weights), "Weights must be finite and non-negative.");

			total += weight;
		}

		if (total <= 0f || !float.IsFinite(total))
			throw new ArgumentException("The total weight must be positive and finite.", nameof(weights));

		var r = NextFloat() * total;
		for (var i = 0; i < list.Length; i++)
		{
			r -= list[i];
			if (r < 0f) return i;
		}

		return list.Length - 1;
	}

	public (ulong State, ulong Inc) GetState() => (_state, _inc);
	public void SetState(ulong state, ulong inc)
	{
		_state = state;
		_inc = inc | 1UL;
	}
}