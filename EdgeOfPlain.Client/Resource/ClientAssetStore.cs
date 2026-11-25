using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Zio;

namespace EdgeOfPlain.Client.Resource;

internal sealed class ClientAssetStore
{
	private readonly Dictionary<Type, IDictionary> _resources = [];

	public void Set<T>(UPath key, T value) where T : class
	{
		if (key.IsNull || key.IsEmpty)
			throw new ArgumentException("An asset key is required.", nameof(key));

		ArgumentNullException.ThrowIfNull(value);

		Map<T>()[key] = value;
	}

	public bool TryGet<T>(UPath key, out T value) where T : class
	{
		if (_resources.TryGetValue(typeof(T), out var map) && map.Contains(key))
		{
			value = (T)map[key]!;
			return true;
		}

		value = null!;
		return false;
	}

	public IReadOnlyDictionary<UPath, T> All<T>() where T : class => Map<T>();

	public IEnumerable<object> Values()
	{
		return _resources.Values.SelectMany(map => map.Values.OfType<object>());
	}

	public void Clear() => _resources.Clear();

	private Dictionary<UPath, T> Map<T>() where T : class
	{
		if (_resources.TryGetValue(typeof(T), out var existing)) return (Dictionary<UPath, T>)existing;

		var map = new Dictionary<UPath, T>(UPathComparer.Ordinal);
		_resources[typeof(T)] = map;

		return map;
	}
}