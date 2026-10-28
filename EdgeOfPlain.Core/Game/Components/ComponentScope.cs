using System;
using System.Collections.Generic;
using EdgeOfPlain.Core.Definitions;

namespace EdgeOfPlain.Core.Game.Components;

public abstract class ComponentScope
{
	public abstract bool IsAll { get; }

	public abstract bool Matches(Type defType);
}

public sealed class ComponentScope<TDef> : ComponentScope where TDef : EntityDef
{
	private static readonly Dictionary<Type, ComponentScope<TDef>> _byType = [];

	private readonly Type? _defType;

	private ComponentScope(Type? defType) => _defType = defType;

	public static ComponentScope<TDef> All { get; } = new(null);

	public static ComponentScope<TDef> ForDefType()
	{
		var type = typeof(TDef);

		if (_byType.TryGetValue(type, out var scope)) return scope;

		var created = new ComponentScope<TDef>(type);
		_byType.Add(type, created);
		return created;
	}

	public override bool IsAll => _defType is null;

	public override bool Matches(Type defType) => _defType is null || _defType.IsAssignableFrom(defType);
}
