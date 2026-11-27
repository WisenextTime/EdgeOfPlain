using System;
using System.Collections.Generic;

namespace EdgeOfPlain.Core.Net
{
	public enum BattleRoomState { Lobby, Loading, Running, Synchronizing }

	public enum RoomPlayerKind : byte { Human = 0, Ai = 1 }

	public readonly record struct RoomPlayer(int Id, string Name, int Team, RoomPlayerKind Kind);

	public sealed class BattleRoom
	{
		private readonly List<RoomPlayer> _players = [];

		public BattleRoom(string modChecksum, BattleRoomOptions? options = null)
		{
			ArgumentNullException.ThrowIfNull(modChecksum);
			TurnProtocol.ValidateChecksum(modChecksum);
			Options = options ?? BattleRoomOptions.Default;
			Options.Validate();
			ModChecksum = modChecksum;
			Players = _players.AsReadOnly();
		}

		public string ModChecksum { get; }
		public BattleRoomOptions Options { get; internal set; }
		public IReadOnlyList<RoomPlayer> Players { get; }
		public int Count => _players.Count;

		public BattleRoomState State { get; internal set; }
		public int? AdministratorId { get; internal set; }

		public RoomPlayer? Find(int id)
		{
			foreach (var player in _players)
				if (player.Id == id) return player;
			return null;
		}

		internal void AddPlayer(int id, string name, int team, RoomPlayerKind kind)
			=> _players.Add(new RoomPlayer(id, name, team, kind));

		internal void RemovePlayer(int id) => _players.RemoveAll(player => player.Id == id);

		internal void SetTeam(int id, int team)
		{
			var index = _players.FindIndex(player => player.Id == id);
			if (index >= 0) _players[index] = _players[index] with { Team = team };
		}

		internal void SetPlayers(IEnumerable<RoomPlayer> players)
		{
			_players.Clear();
			_players.AddRange(players);
		}

		internal void Reset()
		{
			_players.Clear();
			State = BattleRoomState.Lobby;
			AdministratorId = null;
		}
	}
}
