using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using EdgeOfPlain.Core.Utils;

namespace EdgeOfPlain.Core.Framework.Mods;

internal sealed class ModLoadContext : AssemblyLoadContext
{
	internal static readonly IReadOnlySet<string> SharedAssemblies =
		new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"EdgeOfPlain.Core", "Tomlyn", "Arch", "Arch.LowLevel", "Collections.Pooled",
		};

	private readonly List<AssemblyDependencyResolver> _resolvers = [];
	private readonly string _directory;

	public ModLoadContext(string modId, string directory, IEnumerable<string> mainAssemblies)
		: base(modId, isCollectible: true)
	{
		_directory = directory;

		foreach (var assembly in mainAssemblies)
		{
			var manifest = Path.ChangeExtension(assembly, ".deps.json");
			if (File.Exists(manifest)) _resolvers.Add(new AssemblyDependencyResolver(assembly));
		}
	}

	protected override Assembly? Load(AssemblyName assemblyName)
	{
		var name = assemblyName.Name;
		if (name is null) return null;

		if (SharedAssemblies.Contains(name)) return null;

		foreach (var path in _resolvers.Select(resolver => resolver.ResolveAssemblyToPath(assemblyName)).OfType<string>())
		{
			if (File.Exists(path)) return LoadFromAssemblyPath(path);
			LogUtils.LogMessage(
				$"Dependency manifest of mod '{Name}' points to a missing file: {path}", LogUtils.LogType.Warning);
		}

		var sibling = Path.Combine(_directory, name + ".dll");
		return File.Exists(sibling) ? LoadFromAssemblyPath(sibling) : null;
	}

	protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
	{
		foreach (var path in _resolvers.Select(resolver => resolver.ResolveUnmanagedDllToPath(unmanagedDllName)).OfType<string>())
			return LoadUnmanagedDllFromPath(path);
		return IntPtr.Zero;
	}
}
