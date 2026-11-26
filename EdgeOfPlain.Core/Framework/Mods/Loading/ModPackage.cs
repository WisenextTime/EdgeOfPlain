using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EdgeOfPlain.Core.Framework.IO;
using Tomlyn;
using Zio;
using Zio.FileSystems;

namespace EdgeOfPlain.Core.Framework.Mods.Loading
{
	public sealed class ModPackage : IDisposable
	{
		public const string ManifestName = "mod.toml";
		public const string AssetDirectoryName = "Asset";
		public const string OverriderDirectoryName = "Overrider";

		public static readonly TomlSerializerOptions ManifestOptions = new()
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			PropertyNameCaseInsensitive = true,
		};

		private static readonly UPath _manifestPath = new("/" + ManifestName);
		private static readonly UPath _assetPath = new("/" + AssetDirectoryName);
		private static readonly UPath _overriderPath = new("/" + OverriderDirectoryName);

		private readonly ZipFileSystem _zip;

		private ModPackage(string modId, string zipPath, ZipFileSystem zip, IFileSystem assets, bool hasAssets,
			IReadOnlyDictionary<string, IFileSystem> overriders, ModInfo? info)
		{
			ModId = modId;
			ZipPath = zipPath;
			_zip = zip;
			Assets = assets;
			HasAssets = hasAssets;
			Overriders = overriders;
			Info = info;
		}

		public string ModId { get; }
		public string ZipPath { get; }
		public IFileSystem Root => _zip;

		public IFileSystem Assets { get; }
		public IReadOnlyDictionary<string, IFileSystem> Overriders { get; }

		public ModInfo? Info { get; }

		public bool HasAssets { get; }

		public static ModPackage Open(string zipPath) => Open(zipPath, null);

		public static ModPackage Open(string zipPath, string? modId)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(zipPath);

			var zip = new ZipFileSystem(zipPath, isCaseSensitive: false);
			try
			{
				ModInfo? info = null;
				if (zip.FileExists(_manifestPath))
				{
					var parsed = TomlSerializer.Deserialize<ModInfo>(zip.ReadAllText(_manifestPath), ManifestOptions);
					if (string.IsNullOrEmpty(parsed.Id))
						throw new InvalidDataException($"{ManifestName} in '{zipPath}' has a invalid id");
					info = Normalize(parsed);
				}

				var id = ModFileSystem.NormalizeNamespace(
					modId ?? info?.Id ?? throw new InvalidDataException($"'{zipPath}' do not contain {ManifestName}."));

				if (ModFileSystem.ReservedNamespaces.Contains(id))
					throw new InvalidDataException($"mod '{id}' use a invalid namespace");

				var overriders = new Dictionary<string, IFileSystem>(StringComparer.OrdinalIgnoreCase);
				if (zip.DirectoryExists(_overriderPath))
				{
					foreach (var directory in zip.EnumerateDirectories(_overriderPath, "*", SearchOption.TopDirectoryOnly))
					{
						var target = directory.GetName();
						overriders[ModFileSystem.NormalizeNamespace(target)] =
							new SubFileSystem(zip, directory, owned: false, isCaseSensitive: false);
					}
				}

				var hasAssets = zip.DirectoryExists(_assetPath);
				IFileSystem assets = hasAssets
					? new SubFileSystem(zip, _assetPath, owned: false, isCaseSensitive: false)
					: new MemoryFileSystem();

				return new ModPackage(id, Path.GetFullPath(zipPath), zip, assets, hasAssets, overriders, info);
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
}