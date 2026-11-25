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
		IReadOnlyList<ModAssemblyLoadContext> providers,
		ModAssemblyRegistry registry,
		List<ModAssemblyLoadContext> contexts,
		out bool hasManagedAssemblies,
		out ModAssemblyLoadContext? context)
	{
		hasManagedAssemblies = false;
		context = null;

		var assemblies = ManagedAssemblies(directory);
		if (assemblies.Count == 0) return [];

		hasManagedAssemblies = true;

		var created = new ModAssemblyLoadContext(modId, directory, assemblies, registry);
		context = created;
		contexts.Add(created);

		foreach (var provider in providers) created.AddProvider(provider);
		registry.Add(created);

		var mods = new List<Mod>();
		foreach (var assemblyPath in assemblies)
		{
			var name = ReadAssemblyName(assemblyPath);
			if (name is not null && ModAssemblyLoadContext.IsShared(name))
			{
				LogUtils.Logger(typeof(ModEntryPoints)).LogDebug(
					"Mod '{ModId}': assembly '{AssemblyName}' is provided by the host and will not be loaded from the mod.",
					modId, name);
				continue;
			}

			if (name is not null && TryFindProvider(providers, name) is { } dependency)
			{
				LogUtils.Logger(typeof(ModEntryPoints)).LogInformation(
					"Mod '{ModId}': assembly '{AssemblyName}' is provided by dependency mod '{ProviderModId}' and will not be loaded again.",
					modId, name, dependency.ModId);

				WarnOnBundledVersionMismatch(modId, assemblyPath, name, dependency);
				continue;
			}

			Assembly assembly;
			try
			{
				assembly = created.LoadFromAssemblyPath(assemblyPath);
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

	private static ModAssemblyLoadContext? TryFindProvider(
		IReadOnlyList<ModAssemblyLoadContext> providers,
		string assemblyName)
	{
		foreach (var provider in providers)
		{
			if (provider.Ships(assemblyName)) return provider;
		}

		return null;
	}

	private static void WarnOnBundledVersionMismatch(
		string modId,
		string bundledPath,
		string assemblyName,
		ModAssemblyLoadContext provider)
	{
		if (!provider.TryGetOwnPath(assemblyName, out var providedPath)) return;

		var bundled = ReadAssemblyVersion(bundledPath);
		var provided = ReadAssemblyVersion(providedPath);
		if (bundled is null || provided is null || bundled == provided) return;

		LogUtils.Logger(typeof(ModEntryPoints)).LogWarning(
			"Mod '{ModId}' bundles '{AssemblyName}' {BundledVersion}, but dependency '{ProviderModId}' provides {ProvidedVersion}; the dependency version will be used.",
			modId, assemblyName, bundled, provider.ModId, provided);
	}

	private static Version? ReadAssemblyVersion(string path)
	{
		try
		{
			return AssemblyName.GetAssemblyName(path).Version;
		}
		catch(BadImageFormatException)
		{
			return null;
		}
		catch(FileLoadException)
		{
			return null;
		}
	}

	private static string? ReadAssemblyName(string path)
	{
		try
		{
			return AssemblyName.GetAssemblyName(path).Name;
		}
		catch(BadImageFormatException)
		{
			return null;
		}
		catch(FileLoadException)
		{
			return null;
		}
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