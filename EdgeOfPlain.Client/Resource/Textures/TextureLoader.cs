using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using EdgeOfPlain.Core.Framework.IO;
using EdgeOfPlain.Core.Framework.Mods;
using EdgeOfPlain.Core.Framework.Resource;
using EdgeOfPlain.Core.Utils;
using SFML.Graphics;

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
		var images = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var file in directory.Files)
			if (HasExtension(file, ".png"))
				images[WithoutExtension(file)] = file;

		if (images.Count == 0) return;

		foreach (var file in directory.Files)
		{
			if (!HasExtension(file, ".atlas")) continue;

			if (!images.TryGetValue(WithoutExtension(file), out var texturePath))
			{
				LogUtils.LogMessage(
					$"Atlas '{file}' has no image with the same name in '{directory.Path}'.",
					LogUtils.LogType.Warning);
				continue;
			}

			try
			{
				LoadAtlas(file, texturePath);
			}
			catch(Exception e)
			{
				LogUtils.LogMessage($"Failed to load texture atlas '{file}': {e.Message}", LogUtils.LogType.Error);
			}
		}
	}

	private static void LoadAtlas(string atlasPath, string texturePath)
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

	private static (int Width, int Height) ReadPngSize(string texturePath)
	{
		using var stream = FileIO.OpenRead(texturePath);

		Span<byte> header = stackalloc byte[24];
		stream.ReadExactly(header);

		ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
		if (!header[..8].SequenceEqual(signature))
			throw new InvalidDataException($"'{texturePath}' is not a png image");

		return (BinaryPrimitives.ReadInt32BigEndian(header[16..20]),
			BinaryPrimitives.ReadInt32BigEndian(header[20..24]));
	}

	private static bool HasImageExtension(string path) => HasExtension(path, ".png");

	private static bool HasExtension(string path, string extension)
		=> path.EndsWith(extension, StringComparison.OrdinalIgnoreCase);

	private static string WithoutExtension(string path)
	{
		var dot = path.LastIndexOf('.');
		var separator = path.LastIndexOf('/');

		return dot > separator ? path[..dot] : path;
	}
}
