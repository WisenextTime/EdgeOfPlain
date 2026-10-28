using EdgeOfPlain.Core.Definitions;

namespace EdgeOfPlain.Core.Content.Components;

public struct TeamComponent(int team)
{
	public int Team = team;
	public static TeamComponent CreateComponent(EntityDef _) => new TeamComponent();

}