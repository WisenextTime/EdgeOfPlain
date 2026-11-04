using System;
using System.Collections.Generic;
using FixedMathSharp;

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
		return xorShifted >> rot | xorShifted << (-rot & 31);
	}

	public Fixed64 NextFixed() => Fixed64.FromRaw(NextUint());

	public Fixed64 RangeFixed(Fixed64 min, Fixed64 max) => min + NextFixed() * (max - min);
	public bool Change(Fixed64 p) => p > NextFixed();

	public int Range(int min, int max)
	{
		if (max <= min) return min;

		var range = (ulong)((long)max - min);
		var r = NextUint() * range >> 32;
		return (int)(min + (long)r);
	}

	public Fixed64 NextAngle() => NextFixed() * Fixed64.TwoPi;

	public Vector2d InsideUnitCircle()
	{
		var angle = NextAngle();
		var radius = FixedMath.Sqrt(NextFixed());
		var c = FixedMath.Cos(angle);
		var s = FixedMath.Sin(angle);
		return new Vector2d(c * radius, s * radius);
	}

	public Vector2d InsideCircle(Vector2d center, Fixed64 radius) => center + radius * InsideUnitCircle();

	public Vector2d OnUnitCircle()
	{
		var angle = NextAngle();
		var c = FixedMath.Cos(angle);
		var s = FixedMath.Sin(angle);
		return new Vector2d(c, s);
	}

	public Vector2d OnCircle(Vector2d center, Fixed64 radius) => center + radius * OnUnitCircle();
	public Vector2d InsideRect(Vector2d lt, Vector2d rd) => new(RangeFixed(lt.X, rd.X), RangeFixed(lt.Y, rd.Y));

	public Fixed64 Gaussian() => Gaussian(Fixed64.Zero, Fixed64.One);

	public Fixed64 Gaussian(Fixed64 mean, Fixed64 stDev)
	{
		if (stDev < Fixed64.Zero)
			throw new ArgumentOutOfRangeException(nameof(stDev));

		var u1 = Fixed64.One - NextFixed();
		var u2 = NextFixed();
		var mag = FixedMath.Sqrt(-Fixed64.Two * FixedMath.Ln(u1));
		return mean + stDev * mag * FixedMath.Cos(Fixed64.TwoPi * u2);
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

	public int PickWeightedIndex(IEnumerable<Fixed64> weights)
	{
		ArgumentNullException.ThrowIfNull(weights);

		var list = weights as Fixed64[] ?? [.. weights];

		if (list.Length == 0)
			throw new ArgumentException("At least one weight is required.", nameof(weights));

		var total = Fixed64.Zero;
		foreach (var weight in list)
		{
			if (weight < Fixed64.Zero)
				throw new ArgumentOutOfRangeException(nameof(weights), "Weights must be non-negative.");

			total += weight;
		}

		if (total <= Fixed64.Zero)
			throw new ArgumentException("The total weight must be positive.", nameof(weights));

		var r = NextFixed() * total;
		for (var i = 0; i < list.Length; i++)
		{
			r -= list[i];
			if (r < Fixed64.Zero) return i;
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