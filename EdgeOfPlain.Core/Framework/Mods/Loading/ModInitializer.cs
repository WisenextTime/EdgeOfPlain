using System;
using System.Collections.Generic;
using EdgeOfPlain.Core.Utils;

namespace EdgeOfPlain.Core.Framework.Mods.Loading;

internal static class ModInitializer
{
	public static IReadOnlyList<Mod> Initialize(IReadOnlyList<Mod> ordered)
	{
		ArgumentNullException.ThrowIfNull(ordered);

		var loaded = new List<Mod>(ordered.Count);
		foreach (var mod in ordered)
		{
			var missingDependency = FindMissingDependency(mod, loaded);
			if (missingDependency is not null)
			{
				LogUtils.LogMessage(
					$"Mod '{mod.ModInfo.Id}' will not be loaded: it depends on '{missingDependency}', which was not loaded.",
					LogUtils.LogType.Warning);
				continue;
			}

			if (TryInitialize(mod)) loaded.Add(mod);
		}

		return loaded.AsReadOnly();
	}

	private static bool TryInitialize(Mod mod)
	{
		var context = new ModContext(mod);
		var registerTypes = mod.RegisterTypes ?? [];

		try
		{
			foreach (var registerType in registerTypes) ContentTypeRegistry.RegisterType(registerType);
			mod.Preload();
			mod.InitMod(context);
			return true;
		}
		catch(Exception e)
		{
			LogUtils.LogMessage($"Mod '{mod.ModInfo.Id}' failed to load: {e.Message}", LogUtils.LogType.Error);
			foreach (var registerType in registerTypes) ContentTypeRegistry.TryUnRegisterType(registerType);
			context.Return();
			return false;
		}
		finally
		{
			context.Dispose();
		}
	}

	private static string? FindMissingDependency(Mod mod, IReadOnlyList<Mod> loaded)
	{
		var dependence = mod.ModInfo.Dependence;
		if (dependence is null) return null;

		foreach (var required in dependence)
		{
			if (string.IsNullOrEmpty(required.Id)) continue;

			var found = false;
			foreach (var loadedMod in loaded)
			{
				if (!string.Equals(loadedMod.ModInfo.Id, required.Id, StringComparison.Ordinal)) continue;
				found = true;
				break;
			}

			if (!found) return required.Id;
		}

		return null;
	}
}
