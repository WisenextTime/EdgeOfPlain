using System;

namespace EdgeOfPlain.Core.Net;

public sealed record BattleRoomOptions
{
	public const int DefaultMaxPlayers = 8;
	public const int DefaultMaxPayloadBytes = 4096;
	public static BattleRoomOptions Default { get; } = new();

	public int MaxPlayers { get; init; } = DefaultMaxPlayers;
	public int MaxPayloadBytes { get; init; } = DefaultMaxPayloadBytes;
	public int MaxMapBytes { get; init; } = 4 * 1024 * 1024;
	public int MaxPlayerNameLength { get; init; } = 32;

	internal void Validate()
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(MaxPlayers, 1);
		ArgumentOutOfRangeException.ThrowIfLessThan(MaxPayloadBytes, sizeof(ushort));
		ArgumentOutOfRangeException.ThrowIfLessThan(MaxMapBytes, 1);
		ArgumentOutOfRangeException.ThrowIfLessThan(MaxPlayerNameLength, 1);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(MaxPlayerNameLength, TurnProtocol.MaxStringCharacters);
		if (TurnProtocol.FrameHeaderBytes + (long)MaxPlayers * (2L * sizeof(int) + MaxPayloadBytes) > int.MaxValue)
			throw new ArgumentOutOfRangeException(nameof(MaxPayloadBytes), "The combined frame is too large.");
	}

	internal static bool IsValidPlayerName(string? name, int maxLength)
		=> !string.IsNullOrWhiteSpace(name) && name.Length <= maxLength;
}
