using System;
using EdgeOfPlain.Core.Net;

namespace EdgeOfPlain.Core;

public static class Globals
{
	public static Version GameVersion => new(1, 0, 0);

	private static string _playerName = "Player";

	public static string PlayerName
	{
		get => _playerName;
		set
		{
			var maxLength = TurnProtocol.MaxStringCharacters;
			if (!BattleRoomOptions.IsValidPlayerName(value, maxLength))
				throw new ArgumentException($"Player name must be 1-{maxLength} characters.", nameof(value));
			_playerName = value;
		}
	}

	public static string? ModChecksum { get; internal set; }
}
