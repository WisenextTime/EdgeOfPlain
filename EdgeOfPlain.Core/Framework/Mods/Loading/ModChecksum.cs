using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using EdgeOfPlain.Core.Framework.IO;
using EdgeOfPlain.Core.Game.Commands;

namespace EdgeOfPlain.Core.Framework.Mods.Loading;

public static class ModChecksum
{
	public static string Compute(ModFileSystem assets, IReadOnlyList<Mod> loadedMods,
		IReadOnlyList<ModPackage> loadedPackages)
	{
		ArgumentNullException.ThrowIfNull(assets);
		ArgumentNullException.ThrowIfNull(loadedMods);
		ArgumentNullException.ThrowIfNull(loadedPackages);
		using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

		AddString(hash, Globals.GameVersion.ToString());
		foreach (var mod in loadedMods)
		{
			AddString(hash, mod.ModInfo.Id);
			AddString(hash, mod.ModInfo.Version.ToString());
		}
		foreach (var signature in CommandRegistry.NetworkSignatures) AddString(hash, signature);

		// Include the original package files as well as the effective mounted assets:
		// assemblies and resources hidden by overrides must also match.
		foreach (var package in loadedPackages.OrderBy(package => package.ModId, StringComparer.Ordinal))
		{
			AddString(hash, package.ModId);
			AddFiles(hash, package.Root, "");
		}

		foreach (var (name, overlay) in assets.Namespaces.OrderBy(pair => pair.Key, StringComparer.Ordinal))
		{
			AddString(hash, name);
			AddFiles(hash, overlay, "");
		}

		return Convert.ToHexString(hash.GetHashAndReset());
	}

	private static void AddFiles(IncrementalHash hash, FileSystem system, string root)
	{
		foreach (var path in system.EnumerateFiles(root, "*", SearchOption.AllDirectories)
			         .OrderBy(path => path, StringComparer.Ordinal))
		{
			AddString(hash, path.Replace('\\', '/'));
			using var stream = system.OpenRead(path);
			hash.AppendData(SHA256.HashData(stream));
		}
	}

	private static void AddString(IncrementalHash hash, string value)
	{
		var bytes = Encoding.UTF8.GetBytes(value);
		AddLength(hash, bytes.Length);
		hash.AppendData(bytes);
	}

	private static void AddLength(IncrementalHash hash, long value)
	{
		Span<byte> length = stackalloc byte[sizeof(long)];
		BinaryPrimitives.WriteInt64LittleEndian(length, value);
		hash.AppendData(length);
	}
}