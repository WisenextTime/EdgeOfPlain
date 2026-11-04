using System;
using System.Collections.Generic;
using EdgeOfPlain.Core.Utils;
using Microsoft.Extensions.Logging;

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
				LogUtils.Logger(typeof(ModInitializer)).LogWarning(
					"Mod '{ModInfoId}' will not be loaded: it depends on '{MissingDependency}', which was not loaded.",
					mod.ModInfo.Id, missingDependency);
				continue;
			}

			if (TryInitialize(mod))
			{
				LogUtils.Logger(typeof(ModInitializer)).LogInformation(
					"Mod '{ModInfoId}' initialized.", mod.ModInfo.Id);
				loaded.Add(mod);
			}
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
			LogUtils.Logger(typeof(ModInitializer))
				.LogError("Mod '{ModInfoId}' failed to load: {EMessage}", mod.ModInfo.Id, e.Message);
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