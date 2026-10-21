using System;
using System.Collections.Generic;
using EdgeOfPlain.Client.Resource.Textures;
using EdgeOfPlain.Core.Framework.Resource;

namespace EdgeOfPlain.Client.Resource;

public static class ClientAssetsManager
{
	private static readonly ClientAssetStore _store = new();

	public static IReadOnlyDictionary<string, TextureData> Textures => _store.All<TextureData>();

	public static T Get<T>(string key) where T : class
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);

		if (_store.TryGet<T>(key, out var value)) return value;

		if (!AssetsManager.IsRegistered<T>())
			throw new KeyNotFoundException($"Asset '{key}' of type '{typeof(T).Name}' is not loaded.");

		value = AssetsManager.LoadResource<T>(key);
		if (value is null)
			throw new InvalidOperationException($"Resource operator of '{typeof(T).Name}' returned null for '{key}'.");

		_store.Set(key, value);

		return value;
	}

	public static bool TryGet<T>(string key, out T value) where T : class
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);

		return _store.TryGet(key, out value);
	}

	public static IReadOnlyDictionary<string, T> GetAll<T>() where T : class => _store.All<T>();

	internal static void Add<T>(string key, T value) where T : class => _store.Set(key, value);

	public static void Dispose()
	{
		foreach (var resource in _store.Values())
			(resource as IDisposable)?.Dispose();

		_store.Clear();
	}
}
