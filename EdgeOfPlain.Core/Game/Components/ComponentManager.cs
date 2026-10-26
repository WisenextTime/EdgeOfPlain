using System;
using System.Collections.Generic;
using Arch.Core;
using Arch.Core.Extensions;
using EdgeOfPlain.Core.Definitions;

namespace EdgeOfPlain.Core.Game.Components;

public static class ComponentManager
{
	private readonly record struct PendingComponent(
		Type Type,
		Func<ComponentType> Commit,
		Action<Entity, UnitDef> Apply,
		string Owner);

	private static bool _isSealed;
	private static readonly List<PendingComponent> _pending = [];

	private static Action<Entity, UnitDef>[] _compiled = [];
	private static Signature _signature;

	public static bool IsSealed => _isSealed;

	internal static int PendingCount => _pending.Count;

	private static void ThrowIfSealed()
	{
		if (_isSealed) throw new InvalidOperationException("Component registry is already sealed.");
	}

	internal static void StageComponent<T>(Func<UnitDef, T> factory, string owner) where T : unmanaged
	{
		ArgumentNullException.ThrowIfNull(factory);
		ThrowIfSealed();

		var type = typeof(T);

		foreach (var pending in _pending)
			if (pending.Type == type)
				throw new InvalidOperationException(
					$"Component '{type.Name}' is already registered by '{pending.Owner}'.");

		_pending.Add(new PendingComponent(
			type,
			static () =>
			{
				var componentType = ComponentRegistry.Add<T>();
				ArrayRegistry.Add<T>();
				return componentType;
			},
			(entity, def) => entity.Set(factory(def)),
			owner));
	}

	internal static bool TryUnstageComponent(Type componentType)
	{
		if (_isSealed) return false;

		for (var i = 0; i < _pending.Count; i++)
		{
			if (_pending[i].Type != componentType) continue;

			_pending.RemoveAt(i);
			return true;
		}

		return false;
	}

	public static void Seal()
	{
		if (_isSealed) return;

		var types = new List<ComponentType>(_pending.Count);
		var appliers = new List<Action<Entity, UnitDef>>(_pending.Count);
		var owners = new Dictionary<Type, string>(_pending.Count);

		foreach (var pending in _pending)
		{
			if (owners.TryGetValue(pending.Type, out var owner))
				throw new InvalidOperationException(
					$"Component '{pending.Type.Name}' is already registered by '{owner}'.");

			owners[pending.Type] = pending.Owner;
			types.Add(pending.Commit());
			appliers.Add(pending.Apply);
		}

		_signature = new Signature(types.ToArray());
		_compiled = [.. appliers];

		_pending.Clear();
		_isSealed = true;
	}

	public static Entity CreateEntity(World world, UnitDef def)
	{
		if (!_isSealed) throw new InvalidOperationException("Component registry is not sealed yet.");

		var entity = world.Create(_signature);
		var appliers = _compiled;

		foreach (var t in appliers) t(entity, def);

		return entity;
	}

	internal static void Reset()
	{
		_isSealed = false;
		_pending.Clear();
		_compiled = [];
		_signature = default;
	}
}
