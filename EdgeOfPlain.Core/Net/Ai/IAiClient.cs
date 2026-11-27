namespace EdgeOfPlain.Core.Net.Ai;

public interface IAiClient
{
	byte[] BuildCommands(long tick);
}
