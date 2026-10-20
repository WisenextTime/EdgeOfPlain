using System;
using System.Collections.Generic;
using System.IO;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Framework.IO;
using EdgeOfPlain.Core.Framework.Resource;
using EdgeOfPlain.Core.Utils;

namespace EdgeOfPlain.Core.Framework.Mods;

public readonly record struct ModResourceFile(string Path, ModInfo SourceMod);

public static class ModResourceLoader
{
	public const string TomlContentDirectory = "Contents";

	private static readonly Dictionary<string, Action<ModResourceFile>> _loaders = new(StringComparer.Ordinal)
	{
		[TomlContentDirectory] = LoadTomlContent,
	};

	public static bool TryRegister(string directory, Action<ModResourceFile> loader)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(directory);
		ArgumentNullException.ThrowIfNull(loader);

		return _loaders.TryAdd(directory, loader);
	}

	internal static void LoadAll(IReadOnlyList<Mod> mods)
	{
		ArgumentNullException.ThrowIfNull(mods);

		foreach (var mod in mods) LoadMod(mod);
	}

	private static void LoadMod(Mod mod)
	{
		foreach (var (directory, loader) in _loaders)
			LoadDirectory(mod, directory, loader);
	}

	private static void LoadDirectory(Mod mod, string directory, Action<ModResourceFile> loader)
	{
		var source = $"{Launcher.AssetsHead}://{mod.ModInfo.Id}/{directory}/";

		foreach (var file in FileIO.EnumerateFiles(source, "*", SearchOption.AllDirectories))
		{
			try
			{
				loader(new ModResourceFile(file, mod.ModInfo));
			}
			catch(Exception e)
			{
				LogUtils.LogMessage($"Failed to load '{file}': {e.Message}", LogUtils.LogType.Error);
			}
		}
	}

	private static void LoadTomlContent(ModResourceFile file)
	{
		var content = ContentSerializer.Deserialize<ContentDef>(AssetsManager.LoadResource<string>(file.Path)) ??
		              throw new InvalidDataException("the file does not describe any content");
		content.SourceMod = file.SourceMod;
		ContentStore.Add(content);
	}
}
