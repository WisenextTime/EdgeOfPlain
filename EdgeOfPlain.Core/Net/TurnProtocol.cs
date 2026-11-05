namespace EdgeOfPlain.Core.Net;

internal static class TurnProtocol
{
	public const string ConnectionKey = "EdgeOfPlain.Turns.1";
	public const int MaxPlayers = 8;
	public const int MaxPayloadBytes = 4096;
	public const int MaxMapBytes = 4 * 1024 * 1024;

	public const byte Welcome = 1;
	public const byte Start = 2;
	public const byte Submit = 3;
	public const byte Frame = 4;
	public const byte Join = 5;
	public const byte Reject = 6;
	public const byte Roster = 7;
	public const byte Map = 8;
	public const byte Ready = 9;
}