using System;
using EdgeOfPlain.Core.MathLib;
using FixedMathSharp;
using FixedMathSharp.Geometry;
using Xunit;

namespace EdgeOfPlain.Tests;

public sealed class MathLibTests
{
	[Fact]
	public void CatmullRomHitsControlPointsAtEndpoints()
	{
		var p0 = new Vector2d((Fixed64)0, (Fixed64)0);
		var p1 = new Vector2d((Fixed64)10, (Fixed64)0);
		var p2 = new Vector2d((Fixed64)20, (Fixed64)10);
		var p3 = new Vector2d((Fixed64)30, (Fixed64)10);

		Assert.Equal(p1, Curve.CatmullRom(p0, p1, p2, p3, Fixed64.Zero));
		Assert.Equal(p2, Curve.CatmullRom(p0, p1, p2, p3, Fixed64.One));
	}

	[Fact]
	public void BezierHandlesLinearAndQuadraticCurves()
	{
		var linear = Curve.Bezier(
			(Fixed64)0.5,
			new Vector2d((Fixed64)0, (Fixed64)0),
			new Vector2d((Fixed64)10, (Fixed64)20));
		var quadratic = Curve.Bezier(
			(Fixed64)0.5,
			new Vector2d((Fixed64)0, (Fixed64)0),
			new Vector2d((Fixed64)10, (Fixed64)20),
			new Vector2d((Fixed64)20, (Fixed64)0));

		Assert.Equal(new Vector2d((Fixed64)5, (Fixed64)10), linear);
		Assert.Equal(new Vector2d((Fixed64)10, (Fixed64)10), quadratic);
	}

	[Fact]
	public void BezierTangentIsTheCurveDerivative()
	{
		var linear = Curve.BezierTangent(
			(Fixed64)0.5,
			new Vector2d((Fixed64)0, (Fixed64)0),
			new Vector2d((Fixed64)10, (Fixed64)20));
		var quadratic = Curve.BezierTangent(
			(Fixed64)0.5,
			new Vector2d((Fixed64)0, (Fixed64)0),
			new Vector2d((Fixed64)10, (Fixed64)20),
			new Vector2d((Fixed64)20, (Fixed64)0));

		Assert.Equal(new Vector2d((Fixed64)10, (Fixed64)20), linear);
		Assert.Equal(new Vector2d((Fixed64)20, (Fixed64)0), quadratic);
	}

	[Fact]
	public void BezierRejectsMissingControlPoints()
	{
		Assert.Throws<ArgumentException>(() => Curve.Bezier((Fixed64)0.5));
		Assert.Throws<ArgumentException>(() => Curve.BezierTangent((Fixed64)0.5));
	}

	[Fact]
	public void SinglePointCurveHasNoTangent()
	{
		var point = new Vector2d((Fixed64)3, (Fixed64)4);

		Assert.Equal(point, Curve.Bezier((Fixed64)0.5, point));
		Assert.Equal(Vector2d.Zero, Curve.BezierTangent((Fixed64)0.5, point));
	}

	[Fact]
	public void NormalizedOrZeroActuallyNormalizes()
	{
		var normalized = new Vector2d((Fixed64)3, (Fixed64)4).NormalizedOrZero();
		var zero = Vector2d.Zero.NormalizedOrZero();

		Assert.Equal(0.6, (double)normalized.X, 5);
		Assert.Equal(0.8, (double)normalized.Y, 5);
		Assert.Equal(Vector2d.Zero, zero);
	}

	[Fact]
	public void ProjectingOntoZeroReturnsZero()
	{
		var projected = new Vector2d((Fixed64)3, (Fixed64)4).Project(Vector2d.Zero);

		Assert.Equal(Vector2d.Zero, projected);
	}

	[Fact]
	public void AngleBetweenHandlesZeroVector()
	{
		var angle = Vector2d.AngleBetween(Vector2d.Zero, new Vector2d((Fixed64)1, (Fixed64)0));

		Assert.Equal(Fixed64.Zero, angle);
	}

	[Fact]
	public void RotateUsesClockwisePositiveRotation()
	{
		var rotated = new Vector2d((Fixed64)1, (Fixed64)0).Rotate(Fixed64.Pi / (Fixed64)2);

		Assert.Equal(0.0, (double)rotated.X, 5);
		Assert.Equal(1.0, (double)rotated.Y, 5);
	}

	[Fact]
	public void MoveTowardRejectsNegativeDelta()
	{
		Assert.Throws<ArgumentOutOfRangeException>(
			() => Vector2d.Zero.MoveToward(new Vector2d((Fixed64)1, (Fixed64)1), (Fixed64)(-1)));
		Assert.Throws<ArgumentOutOfRangeException>(
			() => AngleExtension.MoveTowardsAngle(Fixed64.Zero, (Fixed64)1, (Fixed64)(-1)));
	}

	[Fact]
	public void OutInQuadSpansFullRange()
	{
		Assert.Equal(Fixed64.Zero, Easing.OutInQuad(Fixed64.Zero));
		Assert.Equal((Fixed64)0.5, Easing.OutInQuad((Fixed64)0.5));
		Assert.Equal(Fixed64.One, Easing.OutInQuad(Fixed64.One));
		Assert.True(Easing.OutInQuad((Fixed64)0.25) < Easing.OutInQuad((Fixed64)0.5));
		Assert.True(Easing.OutInQuad((Fixed64)0.75) > Easing.OutInQuad((Fixed64)0.5));
	}

	[Fact]
	public void ShortestDeltaWrapsAcrossZero()
	{
		var delta = AngleExtension.ShortestDelta((Fixed64)0.1, Fixed64.TwoPi - (Fixed64)0.1);

		Assert.Equal(-0.2, (double)delta, 3);
	}
}
