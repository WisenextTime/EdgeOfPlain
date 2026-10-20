using System;
using System.Collections.Generic;
using System.Linq;
using EdgeOfPlain.Core.Content;
using EdgeOfPlain.Core.Utils;

namespace EdgeOfPlain.Core.Framework.Mods.Loading;

internal static class ModLoadOrder
{
	private enum DependencyState { Satisfied, Waiting, Broken }

	public static IReadOnlyList<Mod> Resolve(IReadOnlyList<Mod> registered)
	{
		ArgumentNullException.ThrowIfNull(registered);

		var coreMod = ResolveCoreMod(registered);
		var byId = new Dictionary<string, Mod>(registered.Count + 1, StringComparer.Ordinal);
		foreach (var mod in registered) byId[mod.ModInfo.Id] = mod;
		byId[coreMod.ModInfo.Id] = coreMod;

		var ordered = new List<Mod>(registered.Count + 1) { coreMod };
		var placed = new HashSet<string>(StringComparer.Ordinal) { coreMod.ModInfo.Id };
		var skipped = new HashSet<string>(StringComparer.Ordinal);
		var pending = registered.Where(mod => !ReferenceEquals(mod, coreMod)).ToList();
		pending.Sort(CompareIds);

		while (pending.Count > 0)
		{
			var ready = FindReady(pending, placed, skipped, byId);
			if (ready >= 0)
			{
				var mod = pending[ready];
				pending.RemoveAt(ready);
				ordered.Add(mod);
				placed.Add(mod.ModInfo.Id);
				continue;
			}

			var broken = FindBroken(pending, placed, skipped, byId, out var problem);
			if (broken >= 0)
			{
				var mod = pending[broken];
				LogUtils.LogMessage($"Mod '{mod.ModInfo.Id}' will not be loaded: {problem}.", LogUtils.LogType.Error);
				skipped.Add(mod.ModInfo.Id);
				pending.RemoveAt(broken);
				continue;
			}

			LogUtils.LogMessage(
				$"Cyclic mod dependency detected among: {string.Join(", ", pending.Select(mod => mod.ModInfo.Id))};" +
				" these mods will not be loaded.",
				LogUtils.LogType.Error);
			foreach (var mod in pending) skipped.Add(mod.ModInfo.Id);
			break;
		}

		return ordered.AsReadOnly();
	}

	private static Mod ResolveCoreMod(IReadOnlyList<Mod> registered)
	{
		foreach (var mod in registered)
			if (mod is Base)
				return mod;

		return new Base();
	}

	private static int FindReady(
		List<Mod> pending,
		HashSet<string> placed,
		HashSet<string> skipped,
		Dictionary<string, Mod> byId)
	{
		for (var i = 0; i < pending.Count; i++)
			if (GetState(pending[i], placed, skipped, byId, out _) == DependencyState.Satisfied)
				return i;

		return -1;
	}

	private static int FindBroken(
		List<Mod> pending,
		HashSet<string> placed,
		HashSet<string> skipped,
		Dictionary<string, Mod> byId,
		out string? problem)
	{
		for (var i = 0; i < pending.Count; i++)
			if (GetState(pending[i], placed, skipped, byId, out problem) == DependencyState.Broken)
				return i;

		problem = null;
		return -1;
	}

	private static DependencyState GetState(
		Mod mod,
		HashSet<string> placed,
		HashSet<string> skipped,
		Dictionary<string, Mod> byId,
		out string? problem)
	{
		problem = null;
		var dependence = mod.ModInfo.Dependence;
		if (dependence is null) return DependencyState.Satisfied;

		var waiting = false;
		foreach (var required in dependence)
		{
			if (string.IsNullOrEmpty(required.Id))
			{
				problem = "it declares a dependency without an id";
				return DependencyState.Broken;
			}

			if (!byId.TryGetValue(required.Id, out var provider))
			{
				problem = $"it depends on '{required.Id}', which is not registered";
				return DependencyState.Broken;
			}

			if (skipped.Contains(required.Id))
			{
				problem = $"it depends on '{required.Id}', which will not be loaded";
				return DependencyState.Broken;
			}

			if (provider.ModInfo.Version is null || required.Version is null)
			{
				problem = $"it depends on '{required.Id}', but a version is missing on one side";
				return DependencyState.Broken;
			}

			if (provider.ModInfo.Version < required.Version)
			{
				problem = $"it requires '{required.Id}' >= {required.Version}, but {provider.ModInfo.Version} is registered";
				return DependencyState.Broken;
			}

			if (!placed.Contains(required.Id)) waiting = true;
		}

		return waiting ? DependencyState.Waiting : DependencyState.Satisfied;
	}

	private static int CompareIds(Mod left, Mod right)
		=> string.Compare(left.ModInfo.Id, right.ModInfo.Id, StringComparison.Ordinal);
}
