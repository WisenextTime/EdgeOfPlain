using System;
using System.Collections.Generic;

namespace EdgeOfPlain.Core.Net;

public enum BattleRoomState { Lobby, Loading, Running }

public readonly record struct RoomPlayer(int Id, string Name);

public sealed class BattleRoom(string modChecksum)
{
	private readonly List<RoomPlayer> _players = [];

	public string ModChecksum { get; } = modChecksum ?? throw new ArgumentNullException(nameof(modChecksum));
	public IReadOnlyList<RoomPlayer> Players => _players.AsReadOnly();
	public BattleRoomState State { get; internal set; }

	internal void AddPlayer(int id, string name) => _players.Add(new RoomPlayer(id, name));

	internal void SetPlayers(IEnumerable<RoomPlayer> players)
	{
		_players.Clear();
		_players.AddRange(players);
	}

	internal void Reset()
	{
		_players.Clear();
		State = BattleRoomState.Lobby;
	}
}