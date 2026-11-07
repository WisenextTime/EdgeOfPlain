using System;
using EdgeOfPlain.Core.Definitions.Worlds;
using FixedMathSharp;
using Tomlyn.Serialization;

namespace EdgeOfPlain.Core.Definitions;

public abstract class EntityDef(string id) : ContentDef(id)
{
	public const int DefaultCollisionRadius = 16;
	private int _radius = DefaultCollisionRadius;

	public bool EnableCollision { get; set; } = true;
	public int Radius
	{
		get => _radius;
		set
		{
			ArgumentOutOfRangeException.ThrowIfNegative(value);
			_radius = value;
		}
	}

	[TomlRequired]
	public int MaxHealth { get; set; }
	[TomlConverter(typeof(Fixed64Converter))]
	public Fixed64 InitHealth { get; set; } = (Fixed64)1;
	[TomlRequired]
	public string Texture { get; set; } = "";

	public PassageMask MoveType { get; set; } = PassageMask.Ground;
}
