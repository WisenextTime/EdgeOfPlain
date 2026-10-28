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
		Action<Entity, EntityDef> Apply,
		ComponentScope Scope,
		string Owner);

	private sealed class ScopedGroup(ComponentScope scope)
	{

		public ComponentScope Scope { get; } = scope;
		public List<ComponentType> Types { get; } = [];
		public List<Action<Entity, EntityDef>> Appliers { get; } = [];
	}

	private sealed record EntityBlueprint(Signature Signature, Action<Entity, EntityDef>[] Appliers);

	private static bool _isSealed;
	private static readonly List<PendingComponent> _pending = [];

	private static ComponentType[] _baseTypes = [];
	private static Action<Entity, EntityDef>[] _baseAppliers = [];
	private static List<ScopedGroup> _groups = [];
	private static readonly Dictionary<Type, EntityBlueprint> _blueprints = [];

	public static bool IsSealed => _isSealed;

	internal static int PendingCount => _pending.Count;

	private static void ThrowIfSealed()
	{
		if (_isSealed) throw new InvalidOperationException("Component registry is already sealed.");
	}

	internal static void StageComponent<T>(Func<EntityDef, T> factory, string owner) where T : unmanaged
		=> StageComponent(factory, ComponentScope<EntityDef>.All, owner);

	internal static void StageComponent<TDef, T>(
		Func<TDef, T> factory,
		ComponentScope<TDef> scope,
		string owner)
		where TDef : EntityDef
		where T : unmanaged
	{
		ArgumentNullException.ThrowIfNull(factory);
		ArgumentNullException.ThrowIfNull(scope);
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
			(entity, def) => entity.Set(factory((TDef)def)),
			scope,
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

		var baseTypes = new List<ComponentType>(_pending.Count);
		var baseAppliers = new List<Action<Entity, EntityDef>>(_pending.Count);
		var groups = new List<ScopedGroup>();
		var owners = new Dictionary<Type, string>(_pending.Count);

		foreach (var pending in _pending)
		{
			if (owners.TryGetValue(pending.Type, out var owner))
				throw new InvalidOperationException(
					$"Component '{pending.Type.Name}' is already registered by '{owner}'.");

			owners[pending.Type] = pending.Owner;

			if (pending.Scope.IsAll)
			{
				baseTypes.Add(pending.Commit());
				baseAppliers.Add(pending.Apply);
			}
			else
			{
				ScopedGroup? group = null;

				foreach (var candidate in groups)
					if (ReferenceEquals(candidate.Scope, pending.Scope))
					{
						group = candidate;
						break;
					}

				if (group is null)
				{
					group = new ScopedGroup(pending.Scope);
					groups.Add(group);
				}

				group.Types.Add(pending.Commit());
				group.Appliers.Add(pending.Apply);
			}
		}

		_baseTypes = [.. baseTypes];
		_baseAppliers = [.. baseAppliers];
		_groups = groups;

		_pending.Clear();
		_isSealed = true;
	}

	public static Entity CreateEntity(World world, EntityDef def)
	{
		if (!_isSealed) throw new InvalidOperationException("Component registry is not sealed yet.");

		var defType = def.GetType();

		if (!_blueprints.TryGetValue(defType, out var blueprint))
		{
			blueprint = BuildBlueprint(defType);
			_blueprints.Add(defType, blueprint);
		}

		var entity = world.Create(blueprint.Signature);

		foreach (var applier in blueprint.Appliers) applier(entity, def);

		return entity;
	}

	private static EntityBlueprint BuildBlueprint(Type defType)
	{
		var types = new List<ComponentType>(_baseTypes.Length + _groups.Count);
		types.AddRange(_baseTypes);

		var appliers = new List<Action<Entity, EntityDef>>(_baseAppliers.Length + _groups.Count);
		appliers.AddRange(_baseAppliers);

		foreach (var group in _groups)
		{
			if (!group.Scope.Matches(defType)) continue;

			types.AddRange(group.Types);
			appliers.AddRange(group.Appliers);
		}

		return new EntityBlueprint(new Signature(types.ToArray()), [.. appliers]);
	}

	internal static void Reset()
	{
		_isSealed = false;
		_pending.Clear();
		_baseTypes = [];
		_baseAppliers = [];
		_groups = [];
		_blueprints.Clear();
	}
}
