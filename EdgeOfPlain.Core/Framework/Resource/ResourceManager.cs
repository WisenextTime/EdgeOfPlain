using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EdgeOfPlain.Core.Framework.IO;

namespace EdgeOfPlain.Core.Framework.Resource;

public abstract record ResourceOperator;

public sealed record ResourceOperator<T>(
	Func<Stream, T> Loader,
	Action<Stream, T>? Saver = null,
	Func<Stream, CancellationToken, ValueTask<T>>? AsyncLoader = null,
	Func<Stream, T, CancellationToken, ValueTask>? AsyncSaver = null) : ResourceOperator;

public static class ResourceManager
{
	private static readonly Lock _gate = new();
	private static readonly Dictionary<Type, ResourceOperator> _registry = [];
	private static FrozenDictionary<Type, ResourceOperator> _operators = FrozenDictionary<Type, ResourceOperator>.Empty;

	public static IReadOnlyCollection<Type> RegisteredTypes => _operators.Keys;

	public static void RegisterResourceOperator<T>(ResourceOperator<T> resourceOperator)
	{
		ArgumentNullException.ThrowIfNull(resourceOperator);

		lock (_gate)
		{
			if (!_registry.TryAdd(typeof(T), resourceOperator))
				throw new ArgumentException($"operator of '{typeof(T)}' is already registered", nameof(resourceOperator));

			_operators = _registry.ToFrozenDictionary();
		}
	}

	public static bool IsRegistered<T>() => _operators.ContainsKey(typeof(T));

	public static bool UnregisterResourceOperator<T>()
	{
		lock (_gate)
		{
			if (!_registry.Remove(typeof(T))) return false;

			_operators = _registry.ToFrozenDictionary();
			return true;
		}
	}

	public static void Clear()
	{
		lock (_gate)
		{
			_registry.Clear();
			_operators = FrozenDictionary<Type, ResourceOperator>.Empty;
		}
	}

	public static T LoadResource<T>(string sourcePath)
	{
		var resourceOperator = Operator<T>();
		using var stream = FileIO.OpenRead(sourcePath);
		return resourceOperator.Loader(stream);
	}

	public static async ValueTask<T> LoadResourceAsync<T>(string sourcePath, CancellationToken cancellationToken = default)
	{
		var resourceOperator = Operator<T>();
		await using var stream = await FileIO.OpenReadAsync(sourcePath, cancellationToken).ConfigureAwait(false);

		return resourceOperator.AsyncLoader is { } asyncLoader
			? await asyncLoader(stream, cancellationToken).ConfigureAwait(false)
			: resourceOperator.Loader(stream);
	}

	public static void SaveResource<T>(T resource, string sourcePath)
	{
		var resourceOperator = Operator<T>();
		var saver = resourceOperator.Saver ?? throw UnsupportedSave<T>();

		using var stream = FileIO.OpenWrite(sourcePath);
		saver(stream, resource);
	}

	public static async ValueTask SaveResourceAsync<T>(T resource, string sourcePath, CancellationToken cancellationToken = default)
	{
		var resourceOperator = Operator<T>();
		var asyncSaver = resourceOperator.AsyncSaver;
		var saver = resourceOperator.Saver;

		if (asyncSaver is null && saver is null) throw UnsupportedSave<T>();

		await using var stream = await FileIO.OpenWriteAsync(sourcePath, cancellationToken).ConfigureAwait(false);

		if (asyncSaver is not null)
		{
			await asyncSaver(stream, resource, cancellationToken).ConfigureAwait(false);
			return;
		}

		saver!(stream, resource);
	}

	private static ResourceOperator<T> Operator<T>()
	{
		if (_operators.TryGetValue(typeof(T), out var resourceOperator)) return (ResourceOperator<T>)resourceOperator;

		throw new InvalidOperationException($"no resource operator is registered for '{typeof(T)}'");
	}

	private static NotSupportedException UnsupportedSave<T>()
		=> new($"resource operator of '{typeof(T)}' does not support saving");
}
