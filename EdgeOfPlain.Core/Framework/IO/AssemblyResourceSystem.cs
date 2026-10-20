using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace EdgeOfPlain.Core.Framework.IO;

public sealed class AssemblyResourceSystem : FileSystem
{
	public const string DefaultAssetFolderName = "Assets";

	private readonly Func<string, bool>? _include;
	private readonly FrozenDictionary<string, string> _resources;
	private readonly FrozenSet<string> _directories;
	private readonly FrozenDictionary<string, FileSystemEntry[]> _children;

	public AssemblyResourceSystem(Assembly assembly, string? prefix = null, Func<string, bool>? include = null)
	{
		ArgumentNullException.ThrowIfNull(assembly);

		_include = include;

		Assembly = assembly;
		Prefix = (prefix ?? $"{assembly.GetName().Name}.{DefaultAssetFolderName}").Trim('.', ' ');

		var resources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var name in assembly.GetManifestResourceNames().OrderBy(name => name, StringComparer.Ordinal))
		{
			if (!TryMap(name, out var path)) continue;
			resources.TryAdd(path, name);
		}

		if (resources.Count == 0)
			throw new InvalidOperationException(
				$"Assembly '{assembly.GetName().Name}' has no manifest resource under '{Prefix}'. " +
				$"Available resources ({assembly.GetManifestResourceNames().Length}): {Describe(assembly)}");

		var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { string.Empty };
		foreach (var path in resources.Keys) ResourcePathTree.AddDirectoryChain(directories, path);

		var children = new Dictionary<string, List<FileSystemEntry>>(StringComparer.OrdinalIgnoreCase);
		foreach (var path in directories)
			if (path.Length != 0)
				ResourcePathTree.AddChild(children, path, true);

		foreach (var path in resources.Keys) ResourcePathTree.AddChild(children, path, false);

		_resources = resources.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
		_directories = directories.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
		_children = children.ToFrozenDictionary(
			pair => pair.Key,
			pair => pair.Value.OrderBy(entry => entry.Path, StringComparer.Ordinal).ToArray(),
			StringComparer.OrdinalIgnoreCase);
	}

	public Assembly Assembly { get; }

	public string Prefix { get; }

	public override bool IsReadOnly => true;

	public override bool IsCaseSensitive => false;

	protected override bool FileExistsCore(string path) => _resources.ContainsKey(path);

	protected override bool DirectoryExistsCore(string path) => _directories.Contains(path);

	protected override Stream OpenReadCore(string path)
	{
		if (!_resources.TryGetValue(path, out var name))
			throw new FileNotFoundException($"Assembly '{Assembly.GetName().Name}' has no embedded resource '{path}'", path);

		return Assembly.GetManifestResourceStream(name)
		       ?? throw new FileNotFoundException($"Embedded resource '{name}' could not be opened", path);
	}

	protected override IEnumerable<FileSystemEntry> EnumerateEntriesCore(string path)
		=> _children.TryGetValue(path, out var children) ? children : [];

	private bool TryMap(string resourceName, out string path)
	{
		path = string.Empty;
		var remainder = resourceName;
		if (Prefix.Length == 0) return TryToPath(remainder, out path) && (_include is null || _include(path));
		if (!resourceName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) return false;
		if (resourceName.Length <= Prefix.Length + 1) return false;
		var separator = resourceName[Prefix.Length];
		if (separator is not ('.' or '/')) return false;

		remainder = resourceName[(Prefix.Length + 1)..];

		return TryToPath(remainder, out path) && (_include is null || _include(path));
	}

	private static bool TryToPath(string remainder, out string path)
	{
		path = string.Empty;

		if (remainder.Contains('/') || remainder.Contains('\\'))
			return TryNormalize(remainder.Replace('\\', '/'), out path);

		var segments = remainder.Split('.', StringSplitOptions.RemoveEmptyEntries);
		if (segments.Length < 2) return TryNormalize(remainder, out path);

		var file = $"{segments[^2]}.{segments[^1]}";
		var folders = string.Join('/', segments[..^2]);

		return TryNormalize(folders.Length == 0 ? file : $"{folders}/{file}", out path);
	}

	private static bool TryNormalize(string candidate, out string path)
	{
		path = string.Empty;
		try
		{
			path = NormalizePath(candidate);
			return path.Length != 0;
		}
		catch(ArgumentException)
		{
			return false;
		}
	}

	private static string Describe(Assembly assembly)
	{
		var names = assembly.GetManifestResourceNames();
		if (names.Length == 0) return "<none>";

		const int max = 20;
		var listed = string.Join(", ", names.Take(max));
		return names.Length <= max ? listed : $"{listed}, … (+{names.Length - max} more)";
	}
}