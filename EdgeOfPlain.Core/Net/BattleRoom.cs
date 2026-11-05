using System;
using System.Collections.Generic;

namespace EdgeOfPlain.Core.Net;

public enum BattleRoomState { Lobby, Loading, Running, Synchronizing }

public readonly record struct RoomPlayer(int Id, string Name);

public sealed class BattleRoom
{
	private readonly List<RoomPlayer> _players = [];
	private readonly IReadOnlyList<RoomPlayer> _playerView;

	public BattleRoom(string modChecksum, BattleRoomOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(modChecksum);
		TurnProtocol.ValidateChecksum(modChecksum);
		Options = options ?? BattleRoomOptions.Default;
		Options.Validate();
		ModChecksum = modChecksum;
		_playerView = _players.AsReadOnly();
	}

	public string ModChecksum { get; }
	public BattleRoomOptions Options { get; internal set; }
	public IReadOnlyList<RoomPlayer> Players => _playerView;
	public BattleRoomState State { get; internal set; }
	public int? AdministratorId { get; internal set; }

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
		AdministratorId = null;
	}
}
