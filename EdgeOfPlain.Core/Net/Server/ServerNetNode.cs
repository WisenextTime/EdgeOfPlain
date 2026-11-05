using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using EdgeOfPlain.Core.Definitions.Worlds;
using EdgeOfPlain.Core.Game.Commands;
using LiteNetLib;
using LiteNetLib.Utils;

namespace EdgeOfPlain.Core.Net.Server;

public sealed partial class ServerNetNode : INetNode
{
	private readonly EventBasedNetListener _listener;
	private readonly NetManager _manager;
	private readonly List<NetPeer> _players = [];
	private readonly Dictionary<NetPeer, byte[]> _submissions = [];
	private readonly HashSet<NetPeer> _ready = [];
	private readonly Dictionary<NetPeer, long> _pending = [];
	private readonly NetworkOptions _options;
	private long _phaseStarted;
	private bool _isDisposed;

	public ServerNetNode(int port, int maxPlayers = BattleRoomOptions.DefaultMaxPlayers, string? checksum = null,
		NetworkOptions? networkOptions = null)
		: this(port, new BattleRoomOptions { MaxPlayers = maxPlayers }, checksum, networkOptions) { }

	public ServerNetNode(int port, BattleRoomOptions roomOptions, string? checksum = null,
		NetworkOptions? networkOptions = null)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(port, IPEndPoint.MinPort);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(port, IPEndPoint.MaxPort);
		ArgumentNullException.ThrowIfNull(roomOptions);

		Room = new BattleRoom(checksum ?? Globals.ModChecksum
			?? throw new InvalidOperationException("Load mods before creating a battle room."), roomOptions);
		_options = networkOptions ?? NetworkOptions.Default;
		_listener = new EventBasedNetListener();
		_listener.ConnectionRequestEvent += request =>
		{
			if (Room.State != BattleRoomState.Lobby)
				request.Reject(TurnProtocol.Reject(ConnectionRejection.RoomNotJoinable));
			else if (_players.Count + _pending.Count >= Room.Options.MaxPlayers ||
			         _pending.Count >= (_options.MaxPendingConnections ?? Room.Options.MaxPlayers))
				request.Reject(TurnProtocol.Reject(ConnectionRejection.RoomFull));
			else if (request.AcceptIfKey(TurnProtocol.ConnectionKey) is { } peer)
				_pending.TryAdd(peer, _options.TimeProvider.GetTimestamp());
		};
		_listener.PeerConnectedEvent += OnConnected;
		_listener.PeerDisconnectedEvent += OnDisconnected;
		_listener.NetworkReceiveEvent += OnReceive;
		_manager = _options.CreateManager(_listener);
		try
		{
			if (!_manager.Start(port)) throw new InvalidOperationException($"Cannot listen on UDP port {port}.");
		}
		catch
		{
			_manager.Stop();
			throw;
		}
	}

	public BattleRoom Room { get; }
	public int LocalPort => _manager.LocalPort;
	public int PlayerCount => _players.Count;
	public int PendingConnectionCount => _pending.Count;
	public bool IsStarted => Room.State == BattleRoomState.Running;
	public long NextTick { get; private set; } = TurnProtocol.FirstTick;

	public void PollEvents()
	{
		ThrowIfDisposed();
		_manager.PollEvents();
		CheckTimeouts();
		PollSynchronization();
	}

	public void StartMatch(Map map)
	{
		ThrowIfDisposed();
		if (Room.State != BattleRoomState.Lobby || _players.Count == 0)
			throw new InvalidOperationException("The room cannot start.");

		var writer = TurnProtocol.Map(map, Room.Options);
		Room.State = BattleRoomState.Loading;
		_phaseStarted = _options.TimeProvider.GetTimestamp();
		Broadcast(writer);
	}

	private void OnConnected(NetPeer peer)
	{
		if (Room.State != BattleRoomState.Lobby) Reject(peer, ConnectionRejection.RoomNotJoinable);
		else if (_players.Count >= Room.Options.MaxPlayers) Reject(peer, ConnectionRejection.RoomFull);
	}

	private void OnDisconnected(NetPeer peer, DisconnectInfo info)
	{
		_pending.Remove(peer);
		if (!_players.Contains(peer)) return;
		CloseRoom(ConnectionRejection.RoomClosed, new InvalidOperationException("A player disconnected."));
	}

	private void OnReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
	{
		try
		{
			if (peer.ConnectionState != ConnectionState.Connected) return;
			TurnProtocol.Require(channel == TurnProtocol.Channel && deliveryMethod == TurnProtocol.Delivery);
			TurnProtocol.Require(reader.AvailableBytes >= sizeof(byte));
			var message = (TurnMessage)reader.GetByte();
			switch (message)
			{
				case TurnMessage.Join:
					ReceiveJoin(peer, reader);
					break;
				case TurnMessage.Ready when Room.State == BattleRoomState.Loading && _players.Contains(peer):
					TurnProtocol.RequireEnd(reader);
					_ready.Add(peer);
					if (_ready.Count == _players.Count) BeginTurns();
					break;
				case TurnMessage.Submit when (IsStarted || Room.State == BattleRoomState.Synchronizing) && _players.Contains(peer):
					ReceiveSubmission(peer, reader);
					break;
				default:
					if (!ReceiveSynchronization(peer, message, reader))
						Reject(peer, ConnectionRejection.InvalidPacket);
					break;
			}
		}
		catch(Exception exception) when(TurnProtocol.IsPacketException(exception))
		{
			Reject(peer, ConnectionRejection.InvalidPacket);
		}
		finally
		{
			reader.Recycle();
		}
	}

	private void ReceiveJoin(NetPeer peer, NetPacketReader reader)
	{
		if (Room.State != BattleRoomState.Lobby)
		{
			Reject(peer, ConnectionRejection.RoomNotJoinable);
			return;
		}
		TurnProtocol.Require(!_players.Contains(peer));
		var (checksum, name) = TurnProtocol.ReadJoin(reader, Room.Options);
		if (checksum != Room.ModChecksum ||
		    !BattleRoomOptions.IsValidPlayerName(name, Room.Options.MaxPlayerNameLength) ||
		    Room.Players.Any(player => string.Equals(player.Name, name, StringComparison.Ordinal)))
		{
			Reject(peer, checksum != Room.ModChecksum
				? ConnectionRejection.ModChecksumMismatch : ConnectionRejection.InvalidPlayerName);
			return;
		}

		if (_players.Count >= Room.Options.MaxPlayers)
		{
			Reject(peer, ConnectionRejection.RoomFull);
			return;
		}

		var id = _players.Count;
		_pending.Remove(peer);
		_players.Add(peer);
		Room.AddPlayer(id, name);
		Room.AdministratorId ??= id;
		peer.Send(TurnProtocol.Welcome(id, Room.Options), TurnProtocol.Channel, TurnProtocol.Delivery);
		Broadcast(TurnProtocol.Roster(Room.Players, Room.AdministratorId.Value));
	}

	private void Reject(NetPeer peer, ConnectionRejection reason)
	{
		if (_players.Contains(peer)) CloseRoom(reason, new InvalidOperationException(reason.ToString()));
		else
		{
			_pending.Remove(peer);
			peer.Disconnect(TurnProtocol.Reject(reason));
		}
	}

	public void SetAdministrator(int playerId)
	{
		ThrowIfDisposed();
		if (Room.State != BattleRoomState.Lobby) throw new InvalidOperationException("Change the administrator in the lobby.");
		ArgumentOutOfRangeException.ThrowIfNegative(playerId);
		ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(playerId, _players.Count);
		Room.AdministratorId = playerId;
		Broadcast(TurnProtocol.Roster(Room.Players, playerId));
	}

	private void CloseRoom(ConnectionRejection reason, Exception failure)
	{
		EndSynchronization(failure);
		foreach (var peer in _players) peer.Disconnect(TurnProtocol.Reject(reason));
		foreach (var peer in _pending.Keys) peer.Disconnect(TurnProtocol.Reject(reason));
		_pending.Clear();
		_players.Clear();
		_submissions.Clear();
		_ready.Clear();
		NextTick = TurnProtocol.FirstTick;
		Room.Reset();
	}

	private void CheckTimeouts()
	{
		foreach (var (peer, since) in _pending.ToArray())
			if (_options.TimeProvider.GetElapsedTime(since) >= _options.JoinTimeout)
				Reject(peer, ConnectionRejection.JoinTimeout);
		if (Room.State == BattleRoomState.Loading &&
		    _options.TimeProvider.GetElapsedTime(_phaseStarted) >= _options.LoadingTimeout)
			CloseRoom(ConnectionRejection.LoadingTimeout, new TimeoutException("World loading timed out."));
		else if (IsStarted && _options.TimeProvider.GetElapsedTime(_phaseStarted) >= _options.TurnTimeout)
			CloseRoom(ConnectionRejection.TurnTimeout, new TimeoutException("Command submission timed out."));
	}

	private void Broadcast(NetDataWriter writer)
	{
		foreach (var peer in _players) peer.Send(writer, TurnProtocol.Channel, TurnProtocol.Delivery);
	}

	private void BeginTurns()
	{
		Room.State = BattleRoomState.Running;
		_phaseStarted = _options.TimeProvider.GetTimestamp();
		Broadcast(TurnProtocol.Start(_players.Count));
	}

	private void ReceiveSubmission(NetPeer peer, NetPacketReader reader)
	{
		var (tick, payload) = TurnProtocol.ReadSubmission(reader, Room.Options);
		TurnProtocol.Require(tick == NextTick && !_submissions.ContainsKey(peer));
		if (Room.State == BattleRoomState.Synchronizing) TurnProtocol.Require(CanReceivePausedSubmission(peer));
		NetworkCommands.Validate(payload);
		_submissions.Add(peer, payload);
		if (IsStarted && _submissions.Count == _players.Count) BroadcastFrame();
	}

	private void BroadcastFrame()
	{
		var commands = new PlayerCommands[_players.Count];
		for (var i = 0; i < _players.Count; i++)
		{
			commands[i] = new PlayerCommands(i, _submissions[_players[i]]);
		}

		Broadcast(TurnProtocol.Frame(new TurnFrame(NextTick, commands)));
		_submissions.Clear();
		NextTick++;
		_phaseStarted = _options.TimeProvider.GetTimestamp();
	}

	public void Dispose()
	{
		if (_isDisposed) return;
		_isDisposed = true;
		EndSynchronization(new ObjectDisposedException(nameof(ServerNetNode)));
		_manager.Stop();
		_pending.Clear();
		_players.Clear();
		_submissions.Clear();
		_ready.Clear();
		NextTick = TurnProtocol.FirstTick;
		Room.Reset();
	}

	private void ThrowIfDisposed()
	{
		if (_isDisposed) throw new ObjectDisposedException(nameof(ServerNetNode));
	}
}
