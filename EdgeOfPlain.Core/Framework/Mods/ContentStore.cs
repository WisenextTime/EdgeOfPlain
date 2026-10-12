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

	internal static void Add(ContentDef def)
	{
		if (_frozen is not null) throw new InvalidOperationException("content store is already sealed");

		if (!_byId.TryAdd(def.Id, def)) throw new InvalidOperationException($"duplicate content id '{def.Id}'");
	}

	internal static void Seal() => _frozen = _byId.ToFrozenDictionary(StringComparer.Ordinal);

	public static ContentDef? Get(string id)
		=> _frozen is not null
			? _frozen.TryGetValue(id, out var def) ? def : null
			: _byId.TryGetValue(id, out def) ? def : null;

	internal static bool TryRemove(ContentDef def) => _byId.Remove(def.Id);

	internal static void Remove(ContentDef def)
	{
		if (!_byId.Remove(def.Id)) throw new Exception($"content {def.Id} is not register");
	}


	public static TDef? Get<TDef>(string id) where TDef : ContentDef => Get(id) as TDef;

	public static IEnumerable<TDef> All<TDef>() where TDef : ContentDef
		=> (_frozen is not null ? (IEnumerable<ContentDef>)_frozen.Values : _byId.Values).OfType<TDef>();
}
