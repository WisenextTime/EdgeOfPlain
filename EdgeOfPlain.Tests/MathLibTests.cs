using System;
using System.Numerics;
using EdgeOfPlain.Core.MathLib;
using Xunit;

namespace EdgeOfPlain.Tests;

public sealed class MathLibTests
{
	[Fact]
	public void CatmullRomHitsControlPointsAtEndpoints()
	{
		var p0 = new Vector2(0, 0);
		var p1 = new Vector2(10, 0);
		var p2 = new Vector2(20, 10);
		var p3 = new Vector2(30, 10);

		Assert.Equal(p1, Curve.CatmullRom(p0, p1, p2, p3, 0f));
		Assert.Equal(p2, Curve.CatmullRom(p0, p1, p2, p3, 1f));
	}

	[Fact]
	public void BezierHandlesLinearAndQuadraticCurves()
	{
		var linear = Curve.Bezier(0.5f, new Vector2(0, 0), new Vector2(10, 20));
		var quadratic = Curve.Bezier(0.5f, new Vector2(0, 0), new Vector2(10, 20), new Vector2(20, 0));

		Assert.Equal(new Vector2(5, 10), linear);
		Assert.Equal(new Vector2(10, 10), quadratic);
	}

	[Fact]
	public void BezierTangentIsTheCurveDerivative()
	{
		var linear = Curve.BezierTangent(0.5f, new Vector2(0, 0), new Vector2(10, 20));
		var quadratic = Curve.BezierTangent(0.5f, new Vector2(0, 0), new Vector2(10, 20), new Vector2(20, 0));

		Assert.Equal(new Vector2(10, 20), linear);
		Assert.Equal(new Vector2(20, 0), quadratic);
	}

	[Fact]
	public void BezierRejectsMissingControlPoints()
	{
		Assert.Throws<ArgumentException>(() => Curve.Bezier(0.5f));
		Assert.Throws<ArgumentException>(() => Curve.BezierTangent(0.5f));
	}

	[Fact]
	public void SinglePointCurveHasNoTangent()
	{
		var point = new Vector2(3, 4);

		Assert.Equal(point, Curve.Bezier(0.5f, point));
		Assert.Equal(Vector2.Zero, Curve.BezierTangent(0.5f, point));
	}

	[Fact]
	public void NormalizedOrZeroActuallyNormalizes()
	{
		var normalized = new Vector2(3, 4).NormalizedOrZero();
		var zero = Vector2.Zero.NormalizedOrZero();

		Assert.Equal(0.6f, normalized.X, 5);
		Assert.Equal(0.8f, normalized.Y, 5);
		Assert.Equal(Vector2.Zero, zero);
	}

	[Fact]
	public void ProjectingOntoZeroReturnsZero()
	{
		var projected = new Vector2(3, 4).Project(Vector2.Zero);

		Assert.Equal(Vector2.Zero, projected);
	}

	[Fact]
	public void AngleBetweenHandlesZeroVector()
	{
		var angle = Vector2.AngleBetween(Vector2.Zero, new Vector2(1, 0));

		Assert.Equal(0f, angle);
	}

	[Fact]
	public void RotateUsesClockwisePositiveRotation()
	{
		var rotated = new Vector2(1, 0).Rotate(MathF.PI / 2f);

		Assert.Equal(0f, rotated.X, 5);
		Assert.Equal(1f, rotated.Y, 5);
	}

	[Fact]
	public void MoveTowardRejectsNegativeDelta()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => Vector2.Zero.MoveToward(Vector2.One, -1f));
		Assert.Throws<ArgumentOutOfRangeException>(() => AngleExtension.MoveTowardsAngle(0f, 1f, -1f));
	}

	[Fact]
	public void OutInQuadSpansFullRange()
	{
		Assert.Equal(0f, Easing.OutInQuad(0f), 5);
		Assert.Equal(0.5f, Easing.OutInQuad(0.5f), 5);
		Assert.Equal(1f, Easing.OutInQuad(1f), 5);
		Assert.True(Easing.OutInQuad(0.25f) < Easing.OutInQuad(0.5f));
		Assert.True(Easing.OutInQuad(0.75f) > Easing.OutInQuad(0.5f));
	}

	[Fact]
	public void ShortestDeltaWrapsAcrossZero()
	{
		var delta = AngleExtension.ShortestDelta(0.1f, MathF.Tau - 0.1f);

		Assert.Equal(-0.2f, delta, 3);
	}
}
