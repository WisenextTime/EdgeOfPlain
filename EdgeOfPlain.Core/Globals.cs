using System;
using EdgeOfPlain.Core.Net;

namespace EdgeOfPlain.Core
{
	public static class Globals
	{
		public static Version GameVersion => new(1, 0, 0);

		public static string PlayerName
		{
			get;
			set
			{
				var maxLength = TurnProtocol.MaxStringCharacters;
				if (!BattleRoomOptions.IsValidPlayerName(value, maxLength))
					throw new ArgumentException($"Player name must be 1-{maxLength} characters.", nameof(value));
				field = value;
			}
		} = "Player";

		public static string? ModChecksum { get; internal set; }
	}
}
