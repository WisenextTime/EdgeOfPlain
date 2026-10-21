using System;
using System.Collections;
using System.Collections.Generic;

namespace EdgeOfPlain.Client.Resource;

internal sealed class ClientAssetStore
{
	private readonly Dictionary<Type, IDictionary> _resources = [];

	public void Set<T>(string key, T value) where T : class
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		ArgumentNullException.ThrowIfNull(value);

		Map<T>()[key] = value;
	}

	public bool TryGet<T>(string key, out T value) where T : class
	{
		if (_resources.TryGetValue(typeof(T), out var map) && map.Contains(key))
		{
			value = (T)map[key]!;
			return true;
		}

		value = null!;
		return false;
	}

	public IReadOnlyDictionary<string, T> All<T>() where T : class => Map<T>();

	public IEnumerable<object> Values()
	{
		foreach (var map in _resources.Values)
		foreach (var value in map.Values)
			if (value is not null)
				yield return value;
	}

	public void Clear() => _resources.Clear();

	private Dictionary<string, T> Map<T>() where T : class
	{
		if (_resources.TryGetValue(typeof(T), out var existing)) return (Dictionary<string, T>)existing;

		var map = new Dictionary<string, T>(StringComparer.Ordinal);
		_resources[typeof(T)] = map;

		return map;
	}
}
