using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using EdgeOfPlain.Core.Definitions;

namespace EdgeOfPlain.Core.Framework.Mods;

public static class ContentStore
{
	private static readonly Dictionary<string, ContentDef> _byId = new(StringComparer.Ordinal);
	private static FrozenDictionary<string, ContentDef>? _frozen;

	public static bool IsSealed => _frozen is not null;

	public static int Count => _frozen?.Count ?? _byId.Count;

	internal static void Add(ContentDef def)
	{
		ArgumentNullException.ThrowIfNull(def);
		ThrowIfSealed();

		if (!_byId.TryAdd(def.Id, def))
			throw new InvalidOperationException($"Content id '{def.Id}' is already registered");
	}

	internal static void Seal() => _frozen ??= _byId.ToFrozenDictionary(StringComparer.Ordinal);

	internal static bool TryRemove(ContentDef def)
	{
		ThrowIfSealed();
		return _byId.Remove(def.Id);
	}

	internal static void Remove(ContentDef def)
	{
		ThrowIfSealed();

		if (!_byId.Remove(def.Id)) throw new InvalidOperationException($"Content '{def.Id}' is not registered");
	}

	internal static void Reset()
	{
		_byId.Clear();
		_frozen = null;
	}

	public static ContentDef? Get(string id)
	{
		ArgumentNullException.ThrowIfNull(id);
		return _frozen is not null ? _frozen.GetValueOrDefault(id) : _byId.GetValueOrDefault(id);
	}

	public static TDef? Get<TDef>(string id) where TDef : ContentDef => Get(id) as TDef;

	public static IEnumerable<TDef> All<TDef>() where TDef : ContentDef
		=> (_frozen is not null
			? (IEnumerable<ContentDef>)_frozen.Values
			: _byId.Values.ToArray()).OfType<TDef>();

	private static void ThrowIfSealed()
	{
		if (_frozen is not null) throw new InvalidOperationException("Content store is already sealed");
	}
}