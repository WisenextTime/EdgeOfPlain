using System;
using System.Collections.Generic;
using System.Drawing;

namespace EdgeOfPlain.Core.Framework.Graphics;

public enum NineSliceMode
{
	Stretch,
	Tile,
}

public readonly record struct NineSlice(
	Rectangle Source,
	float Left,
	float Top,
	float Right,
	float Bottom,
	NineSliceMode Mode = NineSliceMode.Stretch)
{
	public const int PartCount = 9;

	public const int MaxTiledParts = 4096;

	public static NineSlice Uniform(Rectangle source, float margin, NineSliceMode mode = NineSliceMode.Stretch) =>
		new(source, margin, margin, margin, margin, mode);

	public int Split(RectangleF destination, Span<NineSlicePart> parts)
	{
		if (parts.Length < PartCount)
			throw new ArgumentException($"Nine slice needs {PartCount} slots.", nameof(parts));

		if (!TryMeasure(destination, out var frame)) return 0;

		Span<int> sourceX = [Source.Left, Source.Left + frame.SourceLeft, Source.Right - frame.SourceRight, Source.Right];
		Span<int> sourceY = [Source.Top, Source.Top + frame.SourceTop, Source.Bottom - frame.SourceBottom, Source.Bottom];
		Span<float> destX = [destination.Left, destination.Left + frame.DestLeft, destination.Right - frame.DestRight, destination.Right];
		Span<float> destY = [destination.Top, destination.Top + frame.DestTop, destination.Bottom - frame.DestBottom, destination.Bottom];

		var count = 0;

		for (var row = 0; row < 3; row++)
		for (var column = 0; column < 3; column++)
		{
			var width = sourceX[column + 1] - sourceX[column];
			var height = sourceY[row + 1] - sourceY[row];
			if (width <= 0 || height <= 0) continue;

			var partWidth = destX[column + 1] - destX[column];
			var partHeight = destY[row + 1] - destY[row];
			if (partWidth <= 0f || partHeight <= 0f) continue;

			parts[count++] = new NineSlicePart(
				new Rectangle(sourceX[column], sourceY[row], width, height),
				new RectangleF(destX[column], destY[row], partWidth, partHeight));
		}

		return count;
	}

	public IReadOnlyList<NineSlicePart> Split(RectangleF destination)
	{
		Span<NineSlicePart> parts = stackalloc NineSlicePart[PartCount];
		var count = Split(destination, parts);

		return Copy(parts, count);
	}

	public int CountParts(RectangleF destination)
	{
		if (!TryMeasure(destination, out var frame)) return 0;

		return Mode == NineSliceMode.Tile ? frame.ColumnCount * frame.RowCount : PartCount;
	}

	public int SplitParts(RectangleF destination, Span<NineSlicePart> parts) =>
		Mode == NineSliceMode.Tile ? SplitTiled(destination, parts) : Split(destination, parts);

	public int TiledPartCount(RectangleF destination)
	{
		if (!TryMeasure(destination, out var frame)) return 0;

		return frame.ColumnCount * frame.RowCount;
	}

	public int SplitTiled(RectangleF destination, Span<NineSlicePart> parts)
	{
		if (!TryMeasure(destination, out var frame)) return 0;

		var needed = frame.ColumnCount * frame.RowCount;
		if (parts.Length < needed)
			throw new ArgumentException($"Nine slice needs {needed} slots.", nameof(parts));

		var count = 0;

		for (var row = 0; row < frame.RowCount; row++)
		for (var column = 0; column < frame.ColumnCount; column++)
		{
			if (!frame.TrySegment(column, false, out var sourceX, out var sourceWidth, out var destX, out var destWidth)) continue;
			if (!frame.TrySegment(row, true, out var sourceY, out var sourceHeight, out var destY, out var destHeight)) continue;

			parts[count++] = new NineSlicePart(
				new Rectangle(sourceX, sourceY, sourceWidth, sourceHeight),
				new RectangleF(destX, destY, destWidth, destHeight));
		}

		return count;
	}

	public IReadOnlyList<NineSlicePart> SplitTiled(RectangleF destination)
	{
		var needed = TiledPartCount(destination);
		if (needed == 0) return [];

		var buffer = new NineSlicePart[needed];
		var count = SplitTiled(destination, buffer);

		return Copy(buffer, count);
	}

	private bool TryMeasure(RectangleF destination, out Frame frame)
	{
		frame = default;

		if (destination.Width <= 0f || destination.Height <= 0f) return false;
		if (Source.Width <= 0 || Source.Height <= 0) return false;

		var sourceLeft = ClampMargin(Left, Source.Width / 2);
		var sourceRight = ClampMargin(Right, Source.Width / 2);
		var sourceTop = ClampMargin(Top, Source.Height / 2);
		var sourceBottom = ClampMargin(Bottom, Source.Height / 2);

		var destLeft = sourceLeft * Shrink(sourceLeft + sourceRight, destination.Width);
		var destRight = sourceRight * Shrink(sourceLeft + sourceRight, destination.Width);
		var destTop = sourceTop * Shrink(sourceTop + sourceBottom, destination.Height);
		var destBottom = sourceBottom * Shrink(sourceTop + sourceBottom, destination.Height);

		frame = new Frame(
			Source,
			destination,
			sourceLeft,
			sourceRight,
			sourceTop,
			sourceBottom,
			destLeft,
			destRight,
			destTop,
			destBottom,
			Mode);

		return true;
	}

	private static NineSlicePart[] Copy(ReadOnlySpan<NineSlicePart> parts, int count)
	{
		var result = new NineSlicePart[count];
		parts[..count].CopyTo(result);

		return result;
	}

	private static int ClampMargin(float margin, int available)
	{
		if (float.IsNaN(margin) || margin <= 0f || available <= 0) return 0;

		return (int)MathF.Round(MathF.Min(margin, available));
	}

	private static float Shrink(float needed, float available) => needed > 0f ? MathF.Min(1f, available / needed) : 1f;

	private readonly record struct Frame(
		Rectangle Source,
		RectangleF Destination,
		int SourceLeft,
		int SourceRight,
		int SourceTop,
		int SourceBottom,
		float DestLeft,
		float DestRight,
		float DestTop,
		float DestBottom,
		NineSliceMode Mode)
	{
		public int MiddleSourceX => Source.Width - SourceLeft - SourceRight;

		public int MiddleSourceY => Source.Height - SourceTop - SourceBottom;

		public float MiddleDestX => MathF.Max(0f, Destination.Width - DestLeft - DestRight);

		public float MiddleDestY => MathF.Max(0f, Destination.Height - DestTop - DestBottom);

		private int MiddleColumns => CountTiles(MiddleSourceX, MiddleDestX);

		private int MiddleRows => CountTiles(MiddleSourceY, MiddleDestY);

		public int ColumnCount => 2 + MiddleColumns;

		public int RowCount => 2 + MiddleRows;

		public bool TrySegment(int index, bool vertical, out int sourceStart, out int sourceLength, out float destStart, out float destLength)
		{
			sourceStart = 0;
			sourceLength = 0;
			destStart = 0f;
			destLength = 0f;

			var count = vertical ? RowCount : ColumnCount;
			var tiles = vertical ? MiddleRows : MiddleColumns;
			var sourceOrigin = vertical ? Source.Top : Source.Left;
			var sourceTotal = vertical ? Source.Height : Source.Width;
			var marginStart = vertical ? SourceTop : SourceLeft;
			var marginEnd = vertical ? SourceBottom : SourceRight;
			var middleSource = vertical ? MiddleSourceY : MiddleSourceX;
			var middleDest = vertical ? MiddleDestY : MiddleDestX;
			var destOrigin = vertical ? Destination.Top : Destination.Left;
			var destTotal = vertical ? Destination.Height : Destination.Width;
			var destMarginStart = vertical ? DestTop : DestLeft;
			var destMarginEnd = vertical ? DestBottom : DestRight;

			if (index == 0)
			{
				if (marginStart <= 0) return false;

				sourceStart = sourceOrigin;
				sourceLength = marginStart;
				destStart = destOrigin;
				destLength = destMarginStart;

				return true;
			}

			if (index == count - 1)
			{
				if (marginEnd <= 0) return false;

				sourceStart = sourceOrigin + sourceTotal - marginEnd;
				sourceLength = marginEnd;
				destStart = destOrigin + destTotal - destMarginEnd;
				destLength = destMarginEnd;

				return true;
			}

			if (tiles <= 0) return false;

			var tile = index - 1;
			var offset = tile * middleSource;

			if (Mode == NineSliceMode.Stretch && tiles == 1)
			{
				sourceStart = sourceOrigin + marginStart;
				sourceLength = middleSource;
				destStart = destOrigin + destMarginStart;
				destLength = middleDest;

				return sourceLength > 0 && destLength > 0f;
			}

			var remaining = middleDest - offset;
			if (remaining <= 0f) return false;

			var length = MathF.Min(middleSource, remaining);

			sourceStart = sourceOrigin + marginStart;
			sourceLength = (int)MathF.Round(length);
			destStart = destOrigin + destMarginStart + offset;
			destLength = length;

			return sourceLength > 0 && destLength > 0f;
		}

		private int CountTiles(int middleSource, float middleDest)
		{
			if (middleSource <= 0 || middleDest <= 0f) return 0;

			return Mode == NineSliceMode.Tile ? (int)MathF.Ceiling(middleDest / middleSource) : 1;
		}
	}
}

public readonly record struct NineSlicePart(Rectangle Source, RectangleF Destination);
