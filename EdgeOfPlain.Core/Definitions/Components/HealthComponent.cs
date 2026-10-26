namespace EdgeOfPlain.Core.Definitions.Components;

public struct HealthComponent
{
	public int MaxHealth;
	public int Health;

	public static HealthComponent CreateComponent(UnitDef def)
	{
		return new HealthComponent
		{
			MaxHealth = def.MaxHealth,
			Health = (int)(def.MaxHealth * def.InitHealth),
		};
	}
}