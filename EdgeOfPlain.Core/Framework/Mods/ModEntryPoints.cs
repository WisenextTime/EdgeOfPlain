using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EdgeOfPlain.Core.Utils;

namespace EdgeOfPlain.Core.Framework.Mods;

internal static class ModEntryPoints
{
	public static IReadOnlyList<IMod> Create(
		string modId,
		string directory,
		List<ModLoadContext> contexts,
		out bool hasManagedAssemblies)
	{
		hasManagedAssemblies = false;

		var assemblies = ManagedAssemblies(directory);
		if (assemblies.Count == 0) return [];

		hasManagedAssemblies = true;

		var context = new ModLoadContext(modId, directory, assemblies);
		contexts.Add(context);

		var mods = new List<IMod>();
		foreach (var assemblyPath in assemblies)
		{
			Assembly assembly;
			try
			{
				assembly = context.LoadFromAssemblyPath(assemblyPath);
			}
			catch (Exception e)
			{
				LogUtils.LogMessage(
					$"Mod '{modId}': failed to load assembly '{Path.GetFileName(assemblyPath)}': {e.Message}",
					LogUtils.LogType.Error);
				continue;
			}

			foreach (var type in GetTypes(assembly, modId))
			{
				if (type is not { IsAbstract: false, IsInterface: false }) continue;
				if (!typeof(IMod).IsAssignableFrom(type)) continue;

				try
				{
					if (Activator.CreateInstance(type) is IMod mod) mods.Add(mod);
				}
				catch (Exception e)
				{
					LogUtils.LogMessage(
						$"Mod '{modId}': failed to create entry point '{type.FullName}': {e.Message}",
						LogUtils.LogType.Error);
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
		catch (BadImageFormatException)
		{
			return false;
		}
		catch (FileLoadException)
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
		catch (ReflectionTypeLoadException e)
		{
			foreach (var loaderException in e.LoaderExceptions)
				if (loaderException is not null)
					LogUtils.LogMessage(
						$"Mod '{modId}': type load error in '{assembly.GetName().Name}': {loaderException.Message}",
						LogUtils.LogType.Error);

			return e.Types.Where(type => type is not null)!;
		}
	}
}
