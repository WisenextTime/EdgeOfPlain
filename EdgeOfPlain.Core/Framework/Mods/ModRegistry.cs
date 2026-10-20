using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using EdgeOfPlain.Core.Framework.IO;

namespace EdgeOfPlain.Core.Framework.Mods;

public static class ModRegistry
{
	private static readonly List<Mod> _mods = [];
	private static readonly Dictionary<Mod, ModInfo> _infos = [];
	private static readonly ReadOnlyCollection<Mod> _modsView = _mods.AsReadOnly();
	private static readonly IReadOnlySet<string> _reservedIds = ModFileSystem.ReservedNamespaces;
	private static bool _frozen;

	public static IReadOnlyList<Mod> RegisteredMods => _modsView;

	public static void Register(Mod mod, ModInfo info)
	{
		if (_frozen) throw new InvalidOperationException("Mods are already loaded, the registry can not be changed any more.");
		ArgumentNullException.ThrowIfNull(mod);
		if (string.IsNullOrEmpty(info.Id)) throw new ArgumentException("Mod must have a non-empty id.", nameof(info));
		if (_reservedIds.Contains(info.Id))
			throw new InvalidOperationException($"Mod '{info.Id}' uses an internal reserved id");
		if (_mods.Exists(registered => string.Equals(registered.ModInfo.Id, info.Id, StringComparison.Ordinal)))
			throw new InvalidOperationException($"Mod '{info.Id}' has already been registered");

		_infos.Add(mod, info);
		_mods.Add(mod);
	}

	public static ModInfo GetInfo(Mod mod)
		=> _infos.TryGetValue(mod, out var info)
			? info
			: throw new InvalidOperationException($"Mod '{mod.GetType().FullName}' has not been registered.");

	internal static void Unregister(Mod mod)
	{
		_infos.Remove(mod);
		_mods.Remove(mod);
	}

	internal static void Freeze() => _frozen = true;
}
