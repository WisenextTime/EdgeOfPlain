using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using EdgeOfPlain.Core.Definitions;

namespace EdgeOfPlain.Core.Framework.Mods;

public static class ContentStore
{
	private static readonly Dictionary<ContentKey, ContentDef> _byKey = [];
	private static readonly Dictionary<string, List<ContentKey>> _byLocalId = new(StringComparer.Ordinal);
	private static FrozenDictionary<ContentKey, ContentDef>? _frozenByKey;
	private static FrozenDictionary<string, ContentKey[]>? _frozenByLocalId;

	public static bool IsSealed => _frozenByKey is not null;

	public static int Count => _frozenByKey?.Count ?? _byKey.Count;

	public static IEnumerable<ContentKey> Keys
		=> _frozenByKey?.Keys ?? [.. _byKey.Keys];

	internal static void Add(ContentDef def)
	{
		ArgumentNullException.ThrowIfNull(def);
		ThrowIfSealed();

		var key = def.Key;

		if (!_byKey.TryAdd(key, def))
			throw new InvalidOperationException($"Content '{key}' is already registered");

		if (!_byLocalId.TryGetValue(key.LocalId, out var keys))
		{
			keys = [];
			_byLocalId.Add(key.LocalId, keys);
		}

		keys.Add(key);
	}

	internal static void Seal()
	{
		if (_frozenByKey is not null) return;

		_frozenByKey = _byKey.ToFrozenDictionary();
		_frozenByLocalId = _byLocalId.ToFrozenDictionary(
			pair => pair.Key,
			pair => pair.Value.ToArray(),
			StringComparer.Ordinal);
	}

	internal static bool TryRemove(ContentDef def)
	{
		ThrowIfSealed();
		ArgumentNullException.ThrowIfNull(def);

		var key = def.Key;

		if (!_byKey.Remove(key)) return false;

		if (_byLocalId.TryGetValue(key.LocalId, out var keys))
		{
			keys.Remove(key);

			if (keys.Count == 0) _byLocalId.Remove(key.LocalId);
		}

		return true;
	}

	internal static void Remove(ContentDef def)
	{
		ThrowIfSealed();

		if (!TryRemove(def)) throw new InvalidOperationException($"Content '{def.Key}' is not registered");
	}

	internal static void Reset()
	{
		_byKey.Clear();
		_byLocalId.Clear();
		_frozenByKey = null;
		_frozenByLocalId = null;
	}

	public static ContentDef? Get(ContentKey key)
		=> _frozenByKey is not null
			? _frozenByKey.GetValueOrDefault(key)
			: _byKey.GetValueOrDefault(key);

	public static TDef? Get<TDef>(ContentKey key) where TDef : ContentDef => Get(key) as TDef;

	/// <summary>
	/// Looks up content without a referrer: either a fully qualified key (<c>mod:type/id</c>) or a bare id
	/// that only one mod defines. A bare id defined by several mods throws
	/// <see cref="ContentResolutionException"/> instead of guessing.
	/// </summary>
	public static ContentDef? Get(string reference)
		=> TryResolve(reference, null, typeof(ContentDef), out var def) ? def : null;

	/// <summary>
	/// Looks up a reference written inside <paramref name="source"/>: a bare id is resolved in the
	/// mod that owns <paramref name="source"/>, a qualified key resolves globally.
	/// </summary>
	public static ContentDef? Get(string reference, ContentDef source)
		=> TryResolve(reference, source, typeof(ContentDef), out var def) ? def : null;

	public static TDef? Get<TDef>(string reference) where TDef : ContentDef
		=> TryResolve(reference, null, typeof(TDef), out var def) ? def as TDef : null;

	public static TDef? Get<TDef>(string reference, ContentDef source) where TDef : ContentDef
		=> TryResolve(reference, source, typeof(TDef), out var def) ? def as TDef : null;

	public static ContentDef Resolve(string reference)
		=> ResolveCore(reference, null, typeof(ContentDef));

	public static ContentDef Resolve(string reference, ContentDef source)
		=> ResolveCore(reference, source, typeof(ContentDef));

	public static TDef Resolve<TDef>(string reference) where TDef : ContentDef
		=> (TDef)ResolveCore(reference, null, typeof(TDef));

	public static TDef Resolve<TDef>(string reference, ContentDef source) where TDef : ContentDef
		=> (TDef)ResolveCore(reference, source, typeof(TDef));

	public static bool TryResolve<TDef>(string reference, out TDef? def)
		where TDef : ContentDef
	{
		if (TryResolve(reference, null, typeof(TDef), out var found))
		{
			def = (TDef?)found;
			return true;
		}

		def = null;
		return false;
	}

	public static bool TryResolve<TDef>(string reference, ContentDef source, out TDef? def)
		where TDef : ContentDef
	{
		if (TryResolve(reference, source, typeof(TDef), out var found))
		{
			def = (TDef?)found;
			return true;
		}

		def = null;
		return false;
	}

	public static IEnumerable<TDef> All<TDef>() where TDef : ContentDef
		=> (_frozenByKey is not null
			? (IEnumerable<ContentDef>)_frozenByKey.Values
			: [.. _byKey.Values]).OfType<TDef>();

	private static ContentDef ResolveCore(string reference, ContentDef? source, Type expectedType)
		=> TryResolve(reference, source, expectedType, out var def)
			? def!
			: throw CreateMissing(reference, expectedType);

	private static bool TryResolve(string reference, ContentDef? source, Type expectedType, out ContentDef? def)
	{
		ArgumentNullException.ThrowIfNull(reference);
		ArgumentNullException.ThrowIfNull(expectedType);

		def = null;

		var scope = source?.SourceMod.Id;

		if (!ContentKey.TryParse(reference, out var parsed))
			return TryResolveLocal(reference, null, scope, expectedType, out def);
		var key = parsed.Value;

		if (key.ModId.Length == 0) return TryResolveLocal(key.LocalId, key.Type, scope, expectedType, out def);
		def = Get(key);
		if (def is not null && expectedType.IsInstanceOfType(def)) return true;

		def = null;
		return false;

	}

	private static bool TryResolveLocal(
		string localId,
		string? expectedTypeName,
		string? scopeModId,
		Type expectedType,
		out ContentDef? def)
	{
		def = null;

		if (!TryGetCandidates(localId, out var candidates)) return false;

		var matching = new List<ContentKey>(candidates.Length);

		foreach (var candidate in candidates)
		{
			if (expectedTypeName is not null && !string.Equals(candidate.Type, expectedTypeName, StringComparison.Ordinal))
				continue;

			if (scopeModId is not null && !string.Equals(candidate.ModId, scopeModId, StringComparison.Ordinal))
				continue;

			var candidateDef = Get(candidate);

			if (candidateDef is null || !expectedType.IsInstanceOfType(candidateDef)) continue;

			matching.Add(candidate);
		}

		switch (matching.Count)
		{
			case 0:
				return false;

			case 1:
				def = Get(matching[0]);
				return def is not null;

			default:
				var known = string.Join(", ", matching.Select(key => key.ToString()));

				throw new ContentResolutionException(
					$"Content reference '{localId}' is ambiguous; candidates: {known}. " +
					"Use the fully qualified form 'mod:type/id'.");
		}
	}

	private static bool TryGetCandidates(string localId, out ContentKey[] candidates)
	{
		if (_frozenByLocalId is not null)
		{
			candidates = _frozenByLocalId.TryGetValue(localId, out var frozen) ? frozen : [];
			return candidates.Length != 0;
		}

		candidates = _byLocalId.TryGetValue(localId, out var keys) ? [.. keys] : [];
		return candidates.Length != 0;
	}

	private static ContentResolutionException CreateMissing(string reference, Type expectedType)
	{
		var typeName = ContentTypeRegistry.Discriminator(expectedType);

		if (ContentKey.TryParse(reference, out var parsed) && parsed.Value.ModId.Length != 0)
			return new ContentResolutionException($"Content '{parsed.Value}' is not registered as a {typeName}.");

		return new ContentResolutionException($"Content '{reference}' is not registered as a {typeName}.");
	}

	private static void ThrowIfSealed()
	{
		if (_frozenByKey is not null) throw new InvalidOperationException("Content store is already sealed");
	}
}
