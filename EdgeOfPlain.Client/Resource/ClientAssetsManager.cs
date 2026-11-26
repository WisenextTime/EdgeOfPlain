using System;
using System.Collections.Generic;
using EdgeOfPlain.Client.Resource.Textures;
using EdgeOfPlain.Core.Framework.Resource;
using Zio;

namespace EdgeOfPlain.Client.Resource
{
	public static class ClientAssetsManager
	{
		private static readonly ClientAssetStore _store = new();

		public static IReadOnlyDictionary<UPath, TextureData> Textures => _store.All<TextureData>();

		public static T Get<T>(UPath key) where T : class
		{
			if (key.IsNull || key.IsEmpty)
				throw new ArgumentException("An asset key is required.", nameof(key));

			if (_store.TryGet<T>(key, out var value)) return value;

			if (!AssetsManager.IsRegistered<T>())
				throw new KeyNotFoundException($"Asset '{key}' of type '{typeof(T).Name}' is not loaded.");

			value = AssetsManager.LoadResource<T>(key);
			if (value is null)
				throw new InvalidOperationException($"Resource operator of '{typeof(T).Name}' returned null for '{key}'.");

			_store.Set(key, value);

			return value;
		}

		public static bool TryGet<T>(UPath key, out T value) where T : class
		{
			if (key.IsNull || key.IsEmpty)
				throw new ArgumentException("An asset key is required.", nameof(key));

			return _store.TryGet(key, out value);
		}

		public static IReadOnlyDictionary<UPath, T> GetAll<T>() where T : class => _store.All<T>();

		internal static void Add<T>(UPath key, T value) where T : class => _store.Set(key, value);

		public static void Dispose()
		{
			foreach (var resource in _store.Values())
				(resource as IDisposable)?.Dispose();

			_store.Clear();
		}
	}
}