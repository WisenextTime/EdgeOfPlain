using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EdgeOfPlain.Core.Utils;
using Microsoft.Extensions.Logging;

namespace EdgeOfPlain.Core.Framework.Mods.Loading;

internal static class ModEntryPoints
{
	public static IReadOnlyList<Mod> Create(
		string modId,
		string directory,
		List<ModAssemblyLoadContext> contexts,
		out bool hasManagedAssemblies)
	{
		hasManagedAssemblies = false;

		var assemblies = ManagedAssemblies(directory);
		if (assemblies.Count == 0) return [];

		hasManagedAssemblies = true;

		var context = new ModAssemblyLoadContext(modId, directory, assemblies);
		contexts.Add(context);

		var mods = new List<Mod>();
		foreach (var assemblyPath in assemblies)
		{
			Assembly assembly;
			try
			{
				assembly = context.LoadFromAssemblyPath(assemblyPath);
			}
			catch(Exception e)
			{
				LogUtils.Logger(typeof(ModEntryPoints)).LogError(
					"Mod '{ModId}': failed to load assembly '{GetFileName}': {EMessage}",
					modId, Path.GetFileName(assemblyPath), e.Message);
				continue;
			}

			foreach (var type in GetTypes(assembly, modId))
			{
				if (type is not { IsAbstract: false, IsInterface: false }) continue;
				if (!typeof(Mod).IsAssignableFrom(type)) continue;

				try
				{
					if (Activator.CreateInstance(type) is Mod mod) mods.Add(mod);
				}
				catch(Exception e)
				{
					LogUtils.Logger(typeof(ModEntryPoints)).LogError(
						"Mod '{ModId}': failed to create entry point '{TypeFullName}': {EMessage}",
						modId, type.FullName, e.Message);
				}
			}
		}

		return mods;
	}

	private static List<string> ManagedAssemblies(string directory)
	{
		var assemblies = new List<string>();

		foreach (var file in Directory.EnumerateFiles(directory, "*.dll", SearchOption.AllDirectories))
		{
			var relative = Path.GetRelativePath(directory, file).Replace('\\', '/');
			if (relative.StartsWith("runtimes/", StringComparison.OrdinalIgnoreCase)) continue;
			if (!IsManagedAssembly(file)) continue;

			assemblies.Add(file);
		}

		return assemblies;
	}

	private static bool IsManagedAssembly(string path)
	{
		try
		{
			_ = AssemblyName.GetAssemblyName(path);
			return true;
		}
		catch(BadImageFormatException)
		{
			return false;
		}
		catch(FileLoadException)
		{
			return false;
		}
	}

	private static IEnumerable<Type> GetTypes(Assembly assembly, string modId)
	{
		try
		{
			return assembly.GetTypes();
		}
		catch(ReflectionTypeLoadException e)
		{
			foreach (var loaderException in e.LoaderExceptions)
				if (loaderException is not null)
					LogUtils.Logger(typeof(ModEntryPoints)).LogError(
						"Mod '{ModId}': type load error in '{Name}': {LoaderExceptionMessage}",
						modId, assembly.GetName().Name, loaderException.Message);

			return e.Types.Where(type => type is not null)!;
		}
	}
}