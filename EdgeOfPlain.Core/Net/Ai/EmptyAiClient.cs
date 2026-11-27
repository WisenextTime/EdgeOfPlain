namespace EdgeOfPlain.Core.Net.Ai;

public sealed class EmptyAiClient : IAiClient
{
	public static readonly EmptyAiClient Instance = new();

	private static readonly byte[] _emptyPayload = [0, 0];

	private EmptyAiClient() { }

	public byte[] BuildCommands(long tick) => _emptyPayload;
}
