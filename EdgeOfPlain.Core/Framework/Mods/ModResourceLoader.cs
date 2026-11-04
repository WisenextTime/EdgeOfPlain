using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Framework.IO;
using EdgeOfPlain.Core.Framework.Resource;
using EdgeOfPlain.Core.Utils;
using Microsoft.Extensions.Logging;

namespace EdgeOfPlain.Core.Framework.Mods;

public sealed class ModResourceDirectory
{
	internal ModResourceDirectory(string path, ModInfo sourceMod, IReadOnlyList<string> files)
	{
		Path = path;
		SourceMod = sourceMod;
		Files = files;
	}

	public string Path { get; }

	public ModInfo SourceMod { get; }

	public IReadOnlyList<string> Files { get; }
}

public static class ModResourceLoader
{
	public const string TomlContentDirectory = "Contents";

	private static readonly Dictionary<string, Action<ModResourceDirectory>> _loaders = new(StringComparer.Ordinal)
	{
		[TomlContentDirectory] = LoadTomlContent,
	};

	public static bool TryRegister(string directory, Action<ModResourceDirectory> loader)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(directory);
		ArgumentNullException.ThrowIfNull(loader);

		return _loaders.TryAdd(directory, loader);
	}

	internal static void LoadAll(IReadOnlyList<Mod> mods)
	{
		ArgumentNullException.ThrowIfNull(mods);

		foreach (var mod in mods) LoadMod(mod);

		LogUtils.Logger(typeof(ModResourceLoader)).LogInformation(
			"Resource loading finished: {ModCount} mod(s) processed.", mods.Count);
	}

	private static void LoadMod(Mod mod)
	{
		foreach (var (directory, loader) in _loaders)
		{
			var root = $"{Launcher.AssetsHead}://{mod.ModInfo.Id}/{directory}/";

			foreach (var resourceDirectory in EnumerateDirectories(mod, root))
			{
				try
				{
					loader(resourceDirectory);
				}
				catch(Exception e)
				{
					LogUtils.Logger(typeof(ModResourceLoader)).LogError(
						"Failed to load resources in '{ResourceDirectoryPath}': {EMessage}",
						resourceDirectory.Path, e.Message);
				}
			}
		}
	}

	private static IEnumerable<ModResourceDirectory> EnumerateDirectories(Mod mod, string root)
		=> FileIO.EnumerateFiles(root, "*", SearchOption.AllDirectories)
			.GroupBy(Parent, StringComparer.Ordinal)
			.OrderBy(group => group.Key, StringComparer.Ordinal)
			.Select(group => new ModResourceDirectory(
				$"{group.Key}/",
				mod.ModInfo,
				[.. group.OrderBy(file => file, StringComparer.Ordinal)]));

	private static void LoadTomlContent(ModResourceDirectory directory)
	{
		foreach (var file in directory.Files)
		{
			try
			{
				var content = ContentSerializer.Deserialize<ContentDef>(AssetsManager.LoadResource<string>(file)) ??
				              throw new InvalidDataException("the file does not describe any content");
				content.SourceMod = directory.SourceMod;
				ContentStore.Add(content);
			}
			catch(Exception e)
			{
				LogUtils.Logger(typeof(ModResourceLoader)).LogError("Failed to load '{File}': {EMessage}", file, e.Message);
			}
		}
	}

	private static string Parent(string path)
	{
		var separator = path.LastIndexOf('/');
		return separator < 0 ? string.Empty : path[..separator];
	}
}