using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using EdgeOfPlain.Core.Framework.IO;
using EdgeOfPlain.Core.Framework.Mods;
using EdgeOfPlain.Core.Framework.Resource;
using EdgeOfPlain.Core.Utils;
using Microsoft.Extensions.Logging;
using SFML.Graphics;
using Zio;

namespace EdgeOfPlain.Client.Resource.Textures;

public static class TextureLoader
{
	public const string DirectoryName = "Textures";

	public static bool Register() => ModResourceLoader.TryRegister(DirectoryName, Load);

	public static void Load(ModResourceDirectory directory)
	{
		ClientAssetLoader.LoadFolder<Image>(directory, HasImageExtension);
		LoadAtlases(directory);
	}

	private static void LoadAtlases(ModResourceDirectory directory)
	{
		var images = new Dictionary<UPath, UPath>(UPathComparer.OrdinalIgnoreCase);
		foreach (var file in directory.Files)
			if (HasExtension(file, ".png"))
				images[WithoutExtension(file)] = file;

		if (images.Count == 0) return;

		foreach (var file in directory.Files)
		{
			if (!HasExtension(file, ".atlas")) continue;

			if (!images.TryGetValue(WithoutExtension(file), out var texturePath))
			{
				LogUtils.Logger(typeof(TextureLoader))
					.LogError("Atlas '{File}' has no image with the same name in '{DirectoryPath}'.", file, directory.Path);
				continue;
			}

			try
			{
				LoadAtlas(file, texturePath);
			}
			catch(Exception e)
			{
				LogUtils.Logger(typeof(TextureLoader))
					.LogError("Failed to load texture atlas '{File}': {EMessage}", file, e.Message);
			}
		}
	}

	private static void LoadAtlas(UPath atlasPath, UPath texturePath)
	{
		var atlas = AssetsManager.LoadResource<TextureAtlas>(atlasPath);
		var (width, height) = ReadPngSize(texturePath);
		if (width <= 0 || height <= 0)
			throw new InvalidDataException($"'{texturePath}' has an invalid size");

		foreach (var sprite in atlas.Sprites)
		{
			var source = new Rectangle(sprite.X, sprite.Y, sprite.Width, sprite.Height);
			var uv = new RectangleF(
				sprite.X / (float)width,
				sprite.Y / (float)height,
				sprite.Width / (float)width,
				sprite.Height / (float)height);

			ClientAssetsManager.Add(
				TextureData.MakeKey(WithoutExtension(texturePath), sprite.Id),
				new TextureData(sprite.Id, texturePath, source, uv));
		}
	}

	private static (int Width, int Height) ReadPngSize(UPath texturePath)
	{
		using var stream = FileIO.Root.OpenReadShared(texturePath);

		Span<byte> header = stackalloc byte[24];
		stream.ReadExactly(header);

		ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
		if (!header[..8].SequenceEqual(signature))
			throw new InvalidDataException($"'{texturePath}' is not a png image");

		return (BinaryPrimitives.ReadInt32BigEndian(header[16..20]),
			BinaryPrimitives.ReadInt32BigEndian(header[20..24]));
	}

	private static bool HasImageExtension(UPath path) => HasExtension(path, ".png");

	private static bool HasExtension(UPath path, string extension)
		=> string.Equals(path.GetExtensionWithDot(), extension, StringComparison.OrdinalIgnoreCase);

	private static UPath WithoutExtension(UPath path)
		=> path.GetExtensionWithDot() is { Length: > 0 } extension
			? new UPath(path.FullName[..^extension.Length])
			: path;
}