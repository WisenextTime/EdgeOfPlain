using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EdgeOfPlain.Core.Framework.IO;
using Tomlyn;

namespace EdgeOfPlain.Core.Framework.Mods;

public sealed class ModPackage : IDisposable
{
	public const string ManifestName = "mod.toml";
	public const string AssetDirectoryName = "Asset";
	public const string OverriderDirectoryName = "Overrider";

	public static readonly TomlSerializerOptions ManifestOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
	};

	private readonly ZipFileSystem _zip;

	private ModPackage(string modId, string zipPath, ZipFileSystem zip, FileSystem assets,
		IReadOnlyDictionary<string, FileSystem> overriders, ModInfo? info)
	{
		ModId = modId;
		ZipPath = zipPath;
		_zip = zip;
		Assets = assets;
		Overriders = overriders;
		Info = info;
	}

	public string ModId { get; }
	public string ZipPath { get; }

	/// <summary>The zip root. <c>Asset/</c> and <c>Overrider/</c> are views onto it; the loader extracts assemblies from here.</summary>
	public FileSystem Root => _zip;

	public FileSystem Assets { get; }
	public IReadOnlyDictionary<string, FileSystem> Overriders { get; }

	/// <summary>The manifest, with its id and dependency ids normalized. Null only when no manifest exists.</summary>
	public ModInfo? Info { get; }

	public bool HasAssets => Assets.DirectoryExists(string.Empty);
	public static ModPackage Open(string zipPath) => Open(zipPath, null);
	public static ModPackage Open(string zipPath, string? modId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(zipPath);

		var zip = new ZipFileSystem(zipPath);
		try
		{
			ModInfo? info = null;
			if (zip.FileExists(ManifestName))
			{
				var parsed = TomlSerializer.Deserialize<ModInfo>(zip.ReadAllText(ManifestName), ManifestOptions);
				if (string.IsNullOrEmpty(parsed.Id))
					throw new InvalidDataException($"{ManifestName} in '{zipPath}' has a invalid id");

				// The file system resolves namespaces case-insensitively, so every id entering the mod
				// system is normalized once, here; ModManager can then compare ids ordinally.
				info = Normalize(parsed);
			}

			var id = ModFileSystem.NormalizeNamespace(
				modId ?? info?.Id ?? throw new InvalidDataException($"'{zipPath}' do not contain {ManifestName}."));

			if (ModFileSystem.ReservedNamespaces.Contains(id))
				throw new InvalidDataException($"mod '{id}' use a invalid namespace");

			var overriders = new Dictionary<string, FileSystem>(StringComparer.OrdinalIgnoreCase);
			foreach (var directory in zip.EnumerateDirectories(OverriderDirectoryName))
			{
				var separator = directory.LastIndexOf('/');
				var target = directory[(separator + 1)..];
				overriders[ModFileSystem.NormalizeNamespace(target)] = new SubFileSystem(zip, directory);
			}

			return new ModPackage(id, Path.GetFullPath(zipPath), zip, new SubFileSystem(zip, AssetDirectoryName), overriders, info);
		}
		catch
		{
			zip.Dispose();
			throw;
		}
	}

	private static ModInfo Normalize(ModInfo info)
	{
		var dependence = info.Dependence?
			.Select(required => string.IsNullOrEmpty(required.Id)
				? required
				: required with { Id = ModFileSystem.NormalizeNamespace(required.Id) })
			.ToArray();

		return info with { Id = ModFileSystem.NormalizeNamespace(info.Id), Dependence = dependence };
	}

	public void Dispose() => _zip.Dispose();
}
