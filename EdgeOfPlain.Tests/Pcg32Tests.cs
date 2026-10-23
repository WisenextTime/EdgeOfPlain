using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using EdgeOfPlain.Core.MathLib;
using Xunit;

namespace EdgeOfPlain.Tests;

public sealed class Pcg32Tests
{
	[Fact]
	public void MatchesReferenceOutputs()
	{
		var rng = new Pcg32(42);
		uint[] expected =
		[
			2707161783, 2068313097, 3122475824, 2211639955,
			3215226955, 3421331566, 3217466285, 2167406445
		];

		foreach (var value in expected)
			Assert.Equal(value, rng.NextUint());
	}

	[Fact]
	public void NextDoubleMatchesReferenceOutputs()
	{
		var rng = new Pcg32(42);
		double[] expected = [0.6303102186438938, 0.7270080560068604, 0.7486033647998483, 0.7491247468042271];

		foreach (var value in expected)
			Assert.Equal(value, rng.NextDouble(), 15);
	}

	[Fact]
	public void SameSeedAndSequenceProduceSameSequence()
	{
		var first = new Pcg32(12345, 7);
		var second = new Pcg32(12345, 7);

		for (var i = 0; i < 32; i++)
		{
			Assert.Equal(first.NextUint(), second.NextUint());
			Assert.Equal(first.NextFloat(), second.NextFloat());
		}
	}

	[Fact]
	public void StateCanBeRestored()
	{
		var rng = new Pcg32(7);
		var state = rng.GetState();
		var first = Enumerable.Range(0, 8).Select(_ => rng.NextUint()).ToArray();

		rng.SetState(state.State, state.Inc);

		Assert.Equal(first, Enumerable.Range(0, 8).Select(_ => rng.NextUint()));
	}

	[Fact]
	public void RangeStaysInHalfOpenInterval()
	{
		var rng = new Pcg32(99);
		var values = new HashSet<int>();

		for (var i = 0; i < 1000; i++)
			values.Add(rng.Range(10, 20));

		Assert.All(values, value => Assert.InRange(value, 10, 19));
		Assert.True(values.Count > 1);
	}

	[Fact]
	public void RangeHandlesDegenerateAndReversedBounds()
	{
		var rng = new Pcg32(1);

		Assert.Equal(5, rng.Range(5, 5));
		Assert.Equal(10, rng.Range(10, 5));
		Assert.Equal(0, rng.Range(0, 1));
	}

	[Fact]
	public void RangeCoversEntireIntSpace()
	{
		var rng = new Pcg32(17);

		for (var i = 0; i < 1000; i++)
		{
			var value = rng.Range(int.MinValue, int.MaxValue);
			Assert.InRange(value, int.MinValue, int.MaxValue - 1);
		}
	}

	[Fact]
	public void ShuffleProducesAPermutation()
	{
		var rng = new Pcg32(2026);
		var original = Enumerable.Range(0, 100).ToList();
		var shuffled = original.ToList();

		rng.Shuffle(shuffled);

		Assert.Equal(original.OrderBy(value => value), shuffled.OrderBy(value => value));
		Assert.False(original.SequenceEqual(shuffled));
	}

	[Fact]
	public void WeightedIndexRespectsZeroWeights()
	{
		var rng = new Pcg32(3);

		for (var i = 0; i < 100; i++)
		{
			Assert.Equal(1, rng.PickWeightedIndex([0f, 1f, 0f]));
			Assert.Equal(0, rng.PickWeightedIndex([1f, 0f]));
		}
	}

	[Fact]
	public void WeightedIndexRejectsInvalidWeights()
	{
		var rng = new Pcg32(3);

		Assert.Throws<ArgumentException>(() => rng.PickWeightedIndex([]));
		Assert.Throws<ArgumentException>(() => rng.PickWeightedIndex([0f, 0f]));
		Assert.Throws<ArgumentOutOfRangeException>(() => rng.PickWeightedIndex([-1f, 2f]));
		Assert.Throws<ArgumentOutOfRangeException>(() => rng.PickWeightedIndex([float.NaN]));
		Assert.Throws<ArgumentOutOfRangeException>(() => rng.PickWeightedIndex([float.PositiveInfinity]));
	}

	[Fact]
	public void NextFloatIsInUnitInterval()
	{
		var rng = new Pcg32(8);

		for (var i = 0; i < 10_000; i++)
			Assert.InRange(rng.NextFloat(), 0f, MathF.BitDecrement(1f));
	}

	[Fact]
	public void NextDoubleIsInUnitInterval()
	{
		var rng = new Pcg32(8);

		for (var i = 0; i < 10_000; i++)
			Assert.InRange(rng.NextDouble(), 0d, Math.BitDecrement(1d));
	}

	[Fact]
	public void CircleAndRectHelpersStayWithinBounds()
	{
		var rng = new Pcg32(11);

		for (var i = 0; i < 1000; i++)
		{
			Assert.True(rng.InsideUnitCircle().Length() <= 1f);

			var point = rng.InsideRect(new Vector2(-2, -3), new Vector2(4, 5));
			Assert.InRange(point.X, -2f, 4f);
			Assert.InRange(point.Y, -3f, 5f);

			Assert.InRange(rng.RangeFloat(-1f, 1f), -1f, 1f);
			Assert.InRange(rng.NextAngle(), 0f, MathF.Tau);
		}
	}

	[Fact]
	public void ChangeRespectsCertainProbabilities()
	{
		var rng = new Pcg32(12);

		Assert.False(rng.Change(0f));
		Assert.True(rng.Change(1f));
	}

	[Fact]
	public void GaussianProducesFiniteValues()
	{
		var rng = new Pcg32(13);

		for (var i = 0; i < 1000; i++)
			Assert.True(float.IsFinite(rng.Gaussian()));

		Assert.Throws<ArgumentOutOfRangeException>(() => rng.Gaussian(stDev: -1f));
	}
}
