using System;
using System.Collections.Generic;
using System.Threading;

namespace EdgeOfPlain.Core.Framework.Events;

public sealed class EventBus
{
	private readonly Lock _gate = new();
	private readonly Dictionary<Type, object> _handlers = [];

	public EventSubscription Subscribe<T>(Action<T> handler) where T : notnull
	{
		ArgumentNullException.ThrowIfNull(handler);

		lock (_gate)
		{
			var handlers = _handlers.TryGetValue(typeof(T), out var existing) ? (Handlers<T>)existing : new Handlers<T>();
			var current = handlers.Actions;

			var next = new Action<T>[current.Length + 1];
			Array.Copy(current, next, current.Length);
			next[^1] = handler;
			handlers.Actions = next;

			_handlers[typeof(T)] = handlers;
		}

		return new EventSubscription(() => Remove(handler));
	}

	public void Publish<T>(T message) where T : notnull
	{
		if (!_handlers.TryGetValue(typeof(T), out var boxed)) return;
		var actions = ((Handlers<T>)boxed).Actions;
		foreach (var t in actions) t(message);
		
	}

	public bool HasSubscribers<T>() => _handlers.ContainsKey(typeof(T));

	public int SubscriberCount<T>() where T : notnull => 
		_handlers.TryGetValue(typeof(T), out var boxed) ? ((Handlers<T>)boxed).Actions.Length : 0;

	public void Clear()
	{
		lock (_gate) _handlers.Clear();
	}

	private void Remove<T>(Action<T> handler) where T : notnull
	{
		lock (_gate)
		{
			if (!_handlers.TryGetValue(typeof(T), out var boxed)) return;

			var handlers = (Handlers<T>)boxed;
			var index = Array.IndexOf(handlers.Actions, handler);
			if (index < 0) return;

			if (handlers.Actions.Length == 1)
			{
				_handlers.Remove(typeof(T));
				return;
			}

			var next = new Action<T>[handlers.Actions.Length - 1];
			Array.Copy(handlers.Actions, next, index);
			Array.Copy(handlers.Actions, index + 1, next, index, handlers.Actions.Length - index - 1);
			handlers.Actions = next;
		}
	}

	private sealed class Handlers<T> where T : notnull
	{
		public Action<T>[] Actions = [];
	}
}
public readonly struct EventSubscription(Action? unsubscribe) : IDisposable
{
	private readonly Action? _unsubscribe = unsubscribe;

	public bool IsActive => _unsubscribe is not null;

	public void Dispose() => _unsubscribe?.Invoke();
}
