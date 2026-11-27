namespace EdgeOfPlain.Core.Game.Commands
{
	public interface ITeamCommand : ICommand
	{
		int Team { get; set; }
	}
}
