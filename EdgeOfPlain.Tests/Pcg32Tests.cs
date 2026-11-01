using System;
using System.Collections.Generic;
using System.Linq;
using EdgeOfPlain.Core.MathLib;
using FixedMathSharp;
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
	public void NextFixedMatchesReferenceOutputs()
	{
		var rng = new Pcg32(42);
		uint[] expected = [2707161783, 2068313097, 3122475824, 2211639955];

		foreach (var value in expected)
			Assert.Equal(Fixed64.FromRaw(value), rng.NextFixed());
	}

	[Fact]
	public void SameSeedAndSequenceProduceSameSequence()
	{
		var first = new Pcg32(12345, 7);
		var second = new Pcg32(12345, 7);

		for (var i = 0; i < 32; i++)
		{
			Assert.Equal(first.NextUint(), second.NextUint());
			Assert.Equal(first.NextFixed(), second.NextFixed());
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
			Assert.Equal(1, rng.PickWeightedIndex([Fixed64.Zero, Fixed64.One, Fixed64.Zero]));
			Assert.Equal(0, rng.PickWeightedIndex([Fixed64.One, Fixed64.Zero]));
		}
	}

	[Fact]
	public void WeightedIndexRejectsInvalidWeights()
	{
		var rng = new Pcg32(3);

		Assert.Throws<ArgumentException>(() => rng.PickWeightedIndex([]));
		Assert.Throws<ArgumentException>(() => rng.PickWeightedIndex([Fixed64.Zero, Fixed64.Zero]));
		Assert.Throws<ArgumentOutOfRangeException>(() => rng.PickWeightedIndex([(Fixed64)(-1), (Fixed64)2]));
	}

	[Fact]
	public void NextFixedIsInUnitInterval()
	{
		var rng = new Pcg32(8);

		for (var i = 0; i < 10_000; i++)
		{
			var value = rng.NextFixed();
			Assert.True(value >= Fixed64.Zero && value < Fixed64.One);
		}
	}

	[Fact]
	public void CircleAndRectHelpersStayWithinBounds()
	{
		var rng = new Pcg32(11);

		for (var i = 0; i < 1000; i++)
		{
			Assert.True(rng.InsideUnitCircle().Magnitude <= Fixed64.One);

			var point = rng.InsideRect(
				new Vector2d((Fixed64)(-2), (Fixed64)(-3)),
				new Vector2d((Fixed64)4, (Fixed64)5));
			Assert.True(point.X >= (Fixed64)(-2) && point.X <= (Fixed64)4);
			Assert.True(point.Y >= (Fixed64)(-3) && point.Y <= (Fixed64)5);

			var ranged = rng.RangeFixed((Fixed64)(-1), Fixed64.One);
			Assert.True(ranged >= (Fixed64)(-1) && ranged <= Fixed64.One);

			var angle = rng.NextAngle();
			Assert.True(angle >= Fixed64.Zero && angle <= Fixed64.TwoPi);
		}
	}

	[Fact]
	public void ChangeRespectsCertainProbabilities()
	{
		var rng = new Pcg32(12);

		Assert.False(rng.Change(Fixed64.Zero));
		Assert.True(rng.Change(Fixed64.One));
	}

	[Fact]
	public void GaussianProducesFiniteValues()
	{
		var rng = new Pcg32(13);

		for (var i = 0; i < 1000; i++)
			_ = rng.Gaussian();

		Assert.Throws<ArgumentOutOfRangeException>(() => rng.Gaussian((Fixed64)0, (Fixed64)(-1)));
	}
}
