using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using EdgeOfPlain.Core.Framework.Graphics;
using Xunit;

namespace EdgeOfPlain.Tests;

public sealed class NineSliceTests
{
	private static NineSlice Slice() => new(new Rectangle(0, 0, 48, 48), 16, 16, 16, 16);

	[Fact]
	public void FullSplitProducesNineParts()
	{
		var parts = Slice().Split(new RectangleF(0, 0, 200, 100));

		Assert.Equal(9, parts.Count);
		Assert.Equal(new Rectangle(0, 0, 16, 16), parts[0].Source);
		Assert.Equal(new RectangleF(0, 0, 16, 16), parts[0].Destination);

		Assert.Equal(new Rectangle(16, 0, 16, 16), parts[1].Source);
		Assert.Equal(new RectangleF(16, 0, 168, 16), parts[1].Destination);

		Assert.Equal(new Rectangle(32, 32, 16, 16), parts[8].Source);
		Assert.Equal(new RectangleF(184, 84, 16, 16), parts[8].Destination);
	}

	[Fact]
	public void CenterFollowsDestinationSize()
	{
		var parts = Slice().Split(new RectangleF(10, 20, 120, 90));
		var center = parts[4];

		Assert.Equal(new Rectangle(16, 16, 16, 16), center.Source);
		Assert.Equal(new RectangleF(26, 36, 88, 58), center.Destination);
	}

	[Fact]
	public void MarginsShrinkWhenDestinationIsTooSmall()
	{
		var parts = Slice().Split(new RectangleF(0, 0, 20, 20));

		Assert.Equal(4, parts.Count);
		Assert.All(parts, part => Assert.Equal(new SizeF(10, 10), part.Destination.Size));
		Assert.Equal(new RectangleF(10, 10, 10, 10), parts[3].Destination);
	}

	[Fact]
	public void MarginsNeverScaleUp()
	{
		var parts = Slice().Split(new RectangleF(0, 0, 400, 400));

		Assert.Equal(9, parts.Count);
		Assert.Equal(16f, parts[0].Destination.Width);
		Assert.Equal(368f, parts[4].Destination.Width);
	}

	[Fact]
	public void MarginsAreClampedToHalfOfSource()
	{
		var slice = new NineSlice(new Rectangle(0, 0, 20, 20), 30, 30, 30, 30);
		var parts = slice.Split(new RectangleF(0, 0, 200, 200));

		Assert.Equal(4, parts.Count);
		Assert.Equal(new Rectangle(0, 0, 10, 10), parts[0].Source);
		Assert.Equal(new RectangleF(0, 0, 10, 10), parts[0].Destination);
		Assert.Equal(new RectangleF(190, 190, 10, 10), parts[3].Destination);
	}

	[Fact]
	public void UniformKeepsSpriteRect()
	{
		var sprite = new Rectangle(32, 0, 64, 64);
		var slice = NineSlice.Uniform(sprite, 8);
		var parts = slice.Split(new RectangleF(0, 0, 128, 128));

		Assert.Equal(new Rectangle(32, 0, 8, 8), parts[0].Source);
		Assert.Equal(new Rectangle(32 + 56, 56, 8, 8), parts[8].Source);
	}

	[Fact]
	public void EmptyDestinationDrawsNothing()
	{
		Assert.Empty(Slice().Split(new RectangleF(0, 0, 0, 50)));
		Assert.Empty(Slice().Split(new RectangleF(0, 0, 50, -1)));
	}

	[Fact]
	public void ZeroMarginProducesSinglePart()
	{
		var slice = new NineSlice(new Rectangle(4, 8, 32, 32), 0, 0, 0, 0);
		var part = Assert.Single(slice.Split(new RectangleF(5, 6, 64, 64)));

		Assert.Equal(new Rectangle(4, 8, 32, 32), part.Source);
		Assert.Equal(new RectangleF(5, 6, 64, 64), part.Destination);
	}

	[Fact]
	public void NegativeMarginsAreIgnored()
	{
		var slice = new NineSlice(new Rectangle(0, 0, 32, 32), -8, -8, -8, -8);
		var part = Assert.Single(slice.Split(new RectangleF(0, 0, 32, 32)));

		Assert.Equal(new Rectangle(0, 0, 32, 32), part.Source);
	}

	[Fact]
	public void SplitAgainstTooSmallBufferThrows()
	{
		var parts = new NineSlicePart[4];

		Assert.Throws<ArgumentException>(() => Slice().Split(new RectangleF(0, 0, 10, 10), parts));
	}

	[Fact]
	public void DrawNineSliceSubmitsEveryPart()
	{
		var batch = new RecordingBatch();

		var count = batch.DrawNineSlice("ui://panel", Slice(), new RectangleF(10, 20, 100, 60), layer: 7, color: new ColorF(1, 2, 3, 4));

		Assert.Equal(9, count);
		Assert.Equal(9, batch.Draws.Count);

		var first = batch.Draws[0];
		Assert.Equal("ui://panel", first.Texture);
		Assert.Equal(7u, first.Layer);
		Assert.Equal(new ColorF(1, 2, 3, 4), first.Color);
		Assert.Equal(new Rectangle(0, 0, 16, 16), first.Source);
		Assert.Equal(new Vector2(16, 16), first.Size);
		Assert.Equal(10f, first.X);
		Assert.Equal(20f, first.Y);

		var center = batch.Draws[4];
		Assert.Equal(new Vector2(68, 28), center.Size);
		Assert.Equal(26f, center.X);
		Assert.Equal(36f, center.Y);
	}

	[Fact]
	public void TileModeRepeatsMiddleTiles()
	{
		var slice = Slice() with { Mode = NineSliceMode.Tile };
		var destination = new RectangleF(0, 0, 200, 100);
		var parts = slice.SplitTiled(destination);

		Assert.Equal(91, parts.Count);
		Assert.Equal(slice.CountParts(destination), parts.Count);

		Assert.Equal(new RectangleF(0, 0, 16, 16), parts[0].Destination);
		Assert.Equal(new RectangleF(16, 0, 16, 16), parts[1].Destination);
		Assert.Equal(new RectangleF(32, 0, 16, 16), parts[2].Destination);
		Assert.All(parts, part => Assert.True(destination.Contains(part.Destination), part.Destination.ToString()));
	}

	[Fact]
	public void TileModeCropsTheLastTile()
	{
		var slice = Slice() with { Mode = NineSliceMode.Tile };
		var parts = slice.SplitTiled(new RectangleF(0, 0, 56, 56));

		Assert.Equal(16, parts.Count);

		var firstMiddle = parts[1];
		Assert.Equal(new RectangleF(16, 0, 16, 16), firstMiddle.Destination);

		var lastMiddleColumn = parts[2];
		Assert.Equal(new RectangleF(32, 0, 8, 16), lastMiddleColumn.Destination);
		Assert.Equal(new Rectangle(16, 0, 8, 16), lastMiddleColumn.Source);
		Assert.Equal(new Rectangle(16, 0, 16, 16), parts[1].Source);
	}

	[Fact]
	public void TileModeCoversDestinationWithoutOverlap()
	{
		var slice = Slice() with { Mode = NineSliceMode.Tile };
		var destination = new RectangleF(0, 0, 200, 100);
		var parts = slice.SplitTiled(destination);

		var covered = 0f;
		foreach (var part in parts) covered += part.Destination.Width * part.Destination.Height;

		Assert.Equal(destination.Width * destination.Height, covered, 3);
	}

	[Fact]
	public void TileModeFallsBackToStretchWhenTooManyParts()
	{
		var slice = Slice() with { Mode = NineSliceMode.Tile };
		var batch = new RecordingBatch();

		var count = batch.DrawNineSlice("ui://panel", slice, new RectangleF(0, 0, 100000, 100000));

		Assert.Equal(9, count);
		Assert.Equal(9, batch.Draws.Count);
	}

	[Fact]
	public void TileModeIsUsedByTheExtension()
	{
		var slice = Slice() with { Mode = NineSliceMode.Tile };
		var batch = new RecordingBatch();

		var count = batch.DrawNineSlice("ui://panel", slice, new RectangleF(0, 0, 200, 100));

		Assert.Equal(91, count);
		Assert.Equal(new Vector2(16, 16), batch.Draws[1].Size);
	}

	[Fact]
	public void RenderMetadataCarriesNineSlice()
	{
		var slice = Slice() with { Mode = NineSliceMode.Tile };
		var metadata = new RenderMetadata { Texture = "ui://panel", NineSlice = slice, Size = new Vector2(120, 80) };

		Assert.Equal(slice, metadata.NineSlice);
		Assert.Equal(new Vector2(120, 80), metadata.Size);
	}

	private sealed class RecordingBatch : ISpriteBatch
	{
		public List<RecordedDraw> Draws { get; } = [];

		public void Begin(Camera2D? camera2D = null)
		{
		}

		public void End()
		{
		}

		public void Draw(RenderMetadata metadata, float x, float y)
		{
			Draws.Add(new RecordedDraw(
				metadata.Texture,
				metadata.Shader,
				metadata.Layer,
				metadata.Color,
				metadata.Source,
				metadata.Size,
				x,
				y));
		}

		public void DrawText(string text, int size, float x, float y, uint layer)
		{
		}
	}

	private readonly record struct RecordedDraw(
		string? Texture,
		string? Shader,
		uint Layer,
		ColorF Color,
		Rectangle? Source,
		Vector2? Size,
		float X,
		float Y);
}
