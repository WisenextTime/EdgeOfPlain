using System;

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
			if (string.IsNullOrWhiteSpace(value) || value.Length > 32)
				throw new ArgumentException("Player name must be 1-32 characters.", nameof(value));
			_playerName = value;
		}
	}

	public static string? ModChecksum { get; internal set; }
}