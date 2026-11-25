using System;
using System.Collections.Generic;
using System.Linq;
using EdgeOfPlain.Core.Content;
using EdgeOfPlain.Core.Utils;
using Microsoft.Extensions.Logging;

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
				LogUtils.Logger(typeof(ModLoadOrder)).LogError(
					"Mod '{ModInfoId}' will not be loaded: {Problem}.", mod.ModInfo.Id, problem);
				skipped.Add(mod.ModInfo.Id);
				pending.RemoveAt(broken);
				continue;
			}

			LogUtils.Logger(typeof(ModLoadOrder)).LogError(
				"Cyclic mod dependency detected among: {Join}; these mods will not be loaded.",
				string.Join(", ", pending.Select(mod => mod.ModInfo.Id)));
			foreach (var mod in pending) skipped.Add(mod.ModInfo.Id);
			break;
		}

		LogUtils.Logger(typeof(ModLoadOrder)).LogInformation(
			"Resolved mod load order: {Order}.",
			string.Join(" → ", ordered.Select(mod => mod.ModInfo.Id)));

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
		{
			if (GetState(pending[i], placed, skipped, byId, out _) == DependencyState.Satisfied)
				return i;
		}

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
		{
			if (GetState(pending[i], placed, skipped, byId, out problem) == DependencyState.Broken)
				return i;
		}

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

	public static IReadOnlyList<T> OrderInfos<T>(IReadOnlyList<T> items, Func<T, ModInfo> getInfo)
	{
		ArgumentNullException.ThrowIfNull(items);
		ArgumentNullException.ThrowIfNull(getInfo);

		var byId = new Dictionary<string, T>(items.Count, StringComparer.Ordinal);
		var unidentified = new List<T>();

		foreach (var item in items)
		{
			var id = getInfo(item).Id;
			if (string.IsNullOrEmpty(id)) unidentified.Add(item);
			else byId.TryAdd(id, item);
		}

		var pending = byId.Values.OrderBy(item => getInfo(item).Id, StringComparer.Ordinal).ToList();
		var ordered = new List<T>(items.Count);
		var placed = new HashSet<string>(StringComparer.Ordinal);

		while (pending.Count > 0)
		{
			var ready = FindReadyInfo(pending, byId, placed, getInfo);
			if (ready < 0)
			{
				LogUtils.Logger(typeof(ModLoadOrder)).LogWarning(
					"Cyclic mod dependency detected among packages: {Join}; they will be loaded in id order.",
					string.Join(", ", pending.Select(item => getInfo(item).Id)));
				break;
			}

			var next = pending[ready];
			pending.RemoveAt(ready);
			ordered.Add(next);
			placed.Add(getInfo(next).Id);
		}

		ordered.AddRange(pending);
		ordered.AddRange(unidentified);

		return ordered.AsReadOnly();
	}

	private static int FindReadyInfo<T>(
		List<T> pending,
		Dictionary<string, T> byId,
		HashSet<string> placed,
		Func<T, ModInfo> getInfo)
	{
		for (var i = 0; i < pending.Count; i++)
		{
			if (InfoDependenciesPlaced(getInfo(pending[i]), byId, placed)) return i;
		}

		return -1;
	}

	private static bool InfoDependenciesPlaced<T>(
		ModInfo info,
		Dictionary<string, T> byId,
		HashSet<string> placed)
	{
		if (info.Dependence is not { } dependence) return true;

		foreach (var required in dependence)
		{
			if (string.IsNullOrEmpty(required.Id)) continue;
			if (!byId.ContainsKey(required.Id)) continue;
			if (!placed.Contains(required.Id)) return false;
		}

		return true;
	}
}