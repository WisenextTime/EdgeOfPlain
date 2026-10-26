using System;
using System.Collections.Generic;
using Arch.Core;
using Arch.Core.Extensions;
using EdgeOfPlain.Core.Definitions;

namespace EdgeOfPlain.Core.Game.Components;

public static class ComponentManager
{
	private static bool _isSealed;
	private static readonly List<Action<Entity, UnitDef>> _appliers = [];
	private static readonly List<ComponentType> _types = [];

	private static Action<Entity, UnitDef>[] _compiled = [];
	private static Signature _signature;

	private static void ThrowIfSealed()
	{
		if (_isSealed) throw new InvalidOperationException("Component registry is already sealed.");
	}

	public static void RegisterComponent<T>(Func<UnitDef, T> factory) where T : unmanaged
	{
		ArgumentNullException.ThrowIfNull(factory);
		ThrowIfSealed();
		var type = ComponentRegistry.Add<T>();
		ArrayRegistry.Add<T>();
		_types.Add(type);
		_appliers.Add((entity, def) => entity.Set(factory(def)));
	}

	public static void Seal()
	{
		if (_isSealed) return;
		_isSealed = true;
		_signature = new Signature(_types.ToArray());
		_compiled = [.. _appliers];

		_appliers.Clear();
		_types.Clear();
	}

	public static Entity CreateEntity(World world, UnitDef def)
	{
		if (!_isSealed) throw new InvalidOperationException("Component registry is not sealed yet.");

		var entity = world.Create(_signature);
		var appliers = _compiled;
		foreach (var t in appliers) t(entity, def);
		return entity;
	}
}