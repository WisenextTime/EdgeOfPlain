using System;
using System.Buffers;
using System.Drawing;
using System.Numerics;

namespace EdgeOfPlain.Core.Framework.Graphics;

public static class SpriteBatchExtensions
{
	private const int StackPartLimit = 256;

	public static int DrawNineSlice(
		this ISpriteBatch batch,
		string texture,
		NineSlice slice,
		RectangleF destination,
		uint layer = 0,
		ColorF? color = null,
		string? shader = null)
	{
		ArgumentNullException.ThrowIfNull(batch);
		ArgumentException.ThrowIfNullOrEmpty(texture);

		var metadata = new RenderMetadata
		{
			Texture = texture,
			Layer = layer,
			Color = color ?? ColorF.White,
			Shader = shader,
		};

		return DrawNineSliceParts(batch, metadata, slice, destination);
	}

	public static int DrawNineSlice(this ISpriteBatch batch, RenderMetadata metadata, NineSlice slice, RectangleF destination)
	{
		ArgumentNullException.ThrowIfNull(batch);
		ArgumentNullException.ThrowIfNull(metadata);

		return DrawNineSliceParts(batch, metadata, slice, destination);
	}

	private static int DrawNineSliceParts(ISpriteBatch batch, RenderMetadata metadata, NineSlice slice, RectangleF destination)
	{
		if (slice.Mode == NineSliceMode.Tile && slice.TiledPartCount(destination) > NineSlice.MaxTiledParts)
			return DrawNineSliceParts(batch, metadata, slice with { Mode = NineSliceMode.Stretch }, destination);

		var count = slice.CountParts(destination);
		if (count == 0) return 0;

		if (count <= StackPartLimit)
		{
			Span<NineSlicePart> parts = stackalloc NineSlicePart[count];
			var filled = slice.SplitParts(destination, parts);

			return Submit(batch, metadata, parts, filled);
		}

		var rented = ArrayPool<NineSlicePart>.Shared.Rent(count);
		try
		{
			var filled = slice.SplitParts(destination, rented);

			return Submit(batch, metadata, rented, filled);
		}
		finally
		{
			ArrayPool<NineSlicePart>.Shared.Return(rented);
		}
	}

	private static int Submit(ISpriteBatch batch, RenderMetadata metadata, ReadOnlySpan<NineSlicePart> parts, int count)
	{
		for (var index = 0; index < count; index++)
		{
			var part = parts[index];

			metadata.Source = part.Source;
			metadata.Size = new Vector2(part.Destination.Width, part.Destination.Height);

			batch.Draw(metadata, part.Destination.X, part.Destination.Y);
		}

		return count;
	}
}
