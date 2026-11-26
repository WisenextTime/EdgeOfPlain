using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using EdgeOfPlain.Core.Utils;
using Microsoft.Extensions.Logging;

namespace EdgeOfPlain.Core.Framework.Mods.Loading
{
	internal sealed class ModAssemblyLoadContext : AssemblyLoadContext
	{
		private static readonly IReadOnlySet<string> _sharedAssemblies =
			new HashSet<string>(StringComparer.OrdinalIgnoreCase)
			{
				"EdgeOfPlain.Core", "Tomlyn", "Arch", "Arch.LowLevel", "Collections.Pooled",
			};

		[ThreadStatic]
		private static HashSet<string>? _resolving;

		private readonly Lock _gate = new();
		private readonly List<AssemblyDependencyResolver> _resolvers = [];
		private readonly Dictionary<string, string> _ownAssemblies = new(StringComparer.OrdinalIgnoreCase);
		private readonly List<ModAssemblyLoadContext> _providers = [];
		private readonly ModAssemblyRegistry _registry;
		private readonly string _directory;

		public ModAssemblyLoadContext(
			string modId,
			string directory,
			IReadOnlyList<string> mainAssemblies,
			ModAssemblyRegistry registry)
			: base(modId, true)
		{
			ModId = modId;
			_directory = directory;
			_registry = registry;

			foreach (var assembly in mainAssemblies)
			{
				var manifest = Path.ChangeExtension(assembly, ".deps.json");
				if (File.Exists(manifest)) _resolvers.Add(new AssemblyDependencyResolver(assembly));

				var name = ReadAssemblyName(assembly);
				if (name is not null && !_sharedAssemblies.Contains(name)) _ownAssemblies[name] = assembly;
			}
		}

		public string ModId { get; }

		public IReadOnlyCollection<string> AssemblyNames => _ownAssemblies.Keys;

		public IReadOnlyList<ModAssemblyLoadContext> Providers => _providers;

		public static bool IsShared(string assemblyName) => _sharedAssemblies.Contains(assemblyName);

		public bool TryGetOwnPath(string assemblyName, out string path) => _ownAssemblies.TryGetValue(assemblyName, out path!);

		public void AddProvider(ModAssemblyLoadContext provider)
		{
			ArgumentNullException.ThrowIfNull(provider);
			if (ReferenceEquals(provider, this) || _providers.Contains(provider)) return;

			_providers.Add(provider);
		}

		public bool Ships(string assemblyName) => _ownAssemblies.ContainsKey(assemblyName);

		protected override Assembly? Load(AssemblyName assemblyName) => Resolve(assemblyName);

		internal Assembly? Resolve(AssemblyName assemblyName)
		{
			var name = assemblyName.Name;
			if (name is null) return null;
			if (_sharedAssemblies.Contains(name)) return null;

			_resolving ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			if (!_resolving.Add(name)) return null;

			try
			{
				foreach (var provider in _providers)
				{
					if (!provider.Ships(name)) continue;

					if (provider.TryLoadOwn(assemblyName) is { } provided)
					{
						LogUtils.Logger(typeof(ModAssemblyLoadContext)).LogDebug(
							"Mod '{ModId}' resolves assembly '{AssemblyName}' from its dependency '{ProviderModId}'.",
							ModId, name, provider.ModId);
						return provided;
					}
				}

				if (_registry.TryGetOwner(name, this, out var owner) && owner.TryLoadOwn(assemblyName) is { } provided2)
				{
					LogUtils.Logger(typeof(ModAssemblyLoadContext)).LogDebug(
						"Mod '{ModId}' resolves assembly '{AssemblyName}' from mod '{ProviderModId}'.",
						ModId, name, owner.ModId);
					return provided2;
				}

				return TryLoadOwn(assemblyName);
			}
			finally
			{
				_resolving.Remove(name);
			}
		}

		internal Assembly? TryLoadOwn(AssemblyName assemblyName)
		{
			var name = assemblyName.Name;
			if (name is null) return null;

			lock(_gate)
			{
				foreach (var loaded in Assemblies)
				{
					if (string.Equals(loaded.GetName().Name, name, StringComparison.OrdinalIgnoreCase))
						return loaded;
				}

				if (_ownAssemblies.TryGetValue(name, out var own)) return LoadFromPath(own);

				foreach (var resolver in _resolvers)
				{
					var path = resolver.ResolveAssemblyToPath(assemblyName);
					if (path is null) continue;

					if (File.Exists(path)) return LoadFromPath(path);

					LogUtils.Logger(typeof(ModAssemblyLoadContext)).LogError(
						"Dependency manifest of mod '{ModId}' points to a missing file: {Path}", ModId, path);
				}

				var sibling = Path.Combine(_directory, name + ".dll");
				if (File.Exists(sibling)) return LoadFromPath(sibling);
			}

			return null;

			Assembly LoadFromPath(string path)
			{
				try
				{
					return LoadFromAssemblyPath(path);
				}
				catch(FileLoadException)
				{
					foreach (var loaded in Assemblies)
					{
						if (string.Equals(loaded.GetName().Name, name, StringComparison.OrdinalIgnoreCase))
							return loaded;
					}

					throw;
				}
			}
		}

		protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
		{
			foreach (var path in _resolvers.Select(resolver => resolver.ResolveUnmanagedDllToPath(unmanagedDllName)).OfType<string>())
				return LoadUnmanagedDllFromPath(path);

			return IntPtr.Zero;
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
	}
}