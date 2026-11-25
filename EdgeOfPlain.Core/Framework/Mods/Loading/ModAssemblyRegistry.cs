using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using EdgeOfPlain.Core.Utils;
using Microsoft.Extensions.Logging;

namespace EdgeOfPlain.Core.Framework.Mods.Loading;

internal sealed class ModAssemblyRegistry
{
	private readonly Lock _gate = new();
	private readonly Dictionary<string, ModAssemblyLoadContext> _owners = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, ModAssemblyLoadContext> _contexts = new(StringComparer.Ordinal);

	public void Add(ModAssemblyLoadContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		lock(_gate)
		{
			_contexts[context.ModId] = context;

			foreach (var name in context.AssemblyNames)
			{
				if (_owners.TryGetValue(name, out var existing))
				{
					if (ReferenceEquals(existing, context)) continue;

					LogUtils.Logger(typeof(ModAssemblyRegistry)).LogWarning(
						"Assembly '{AssemblyName}' is shipped by both mod '{ExistingModId}' and mod '{ModId}';" +
						" mods that do not declare a dependency will keep using the first one.",
						name, existing.ModId, context.ModId);
					continue;
				}

				_owners[name] = context;
			}
		}
	}

	public void Remove(ModAssemblyLoadContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		lock(_gate)
		{
			foreach (var name in context.AssemblyNames.ToArray())
			{
				if (_owners.TryGetValue(name, out var owner) && ReferenceEquals(owner, context))
					_owners.Remove(name);
			}

			if (_contexts.TryGetValue(context.ModId, out var registered) && ReferenceEquals(registered, context))
				_contexts.Remove(context.ModId);
		}
	}

	public bool TryGetOwner(string assemblyName, ModAssemblyLoadContext requester, out ModAssemblyLoadContext provider)
	{
		lock(_gate)
		{
			if (_owners.TryGetValue(assemblyName, out var owner) && !ReferenceEquals(owner, requester))
			{
				provider = owner;
				return true;
			}
		}

		provider = null!;
		return false;
	}

	public bool TryGetContext(string modId, out ModAssemblyLoadContext context)
	{
		lock(_gate) return _contexts.TryGetValue(modId, out context!);
	}

	public void Clear()
	{
		lock(_gate)
		{
			_owners.Clear();
			_contexts.Clear();
		}
	}
}