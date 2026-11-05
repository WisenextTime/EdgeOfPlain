using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EdgeOfPlain.Core.Definitions.Worlds;
using LiteNetLib;
using LiteNetLib.Utils;
using Tomlyn;

namespace EdgeOfPlain.Core.Net.Server;

public sealed class ServerNetNode : INetNode
{
	private readonly EventBasedNetListener _listener;
	private readonly NetManager _manager;
	private readonly List<NetPeer> _players = [];
	private readonly Dictionary<NetPeer, byte[]> _submissions = [];
	private readonly HashSet<NetPeer> _ready = [];
	private readonly HashSet<NetPeer> _rejected = [];
	private readonly int _maxPlayers;
	private long _nextTick = 1;
	private bool _isDisposed;

	public ServerNetNode(int port, int maxPlayers = TurnProtocol.MaxPlayers, string? checksum = null)
	{
		if (maxPlayers is < 1 or > TurnProtocol.MaxPlayers)
			throw new ArgumentOutOfRangeException(nameof(maxPlayers));

		Room = new BattleRoom(checksum ?? Globals.ModChecksum
			?? throw new InvalidOperationException("Load mods before creating a battle room."));
		_maxPlayers = maxPlayers;
		_listener = new EventBasedNetListener();
		_listener.ConnectionRequestEvent += request =>
		{
			if (Room.State != BattleRoomState.Lobby || _players.Count >= _maxPlayers) request.Reject();
			else request.AcceptIfKey(TurnProtocol.ConnectionKey);
		};
		_listener.PeerConnectedEvent += OnConnected;
		_listener.PeerDisconnectedEvent += OnDisconnected;
		_listener.NetworkReceiveEvent += OnReceive;
		_manager = new NetManager(_listener);
		if (!_manager.Start(port)) throw new InvalidOperationException($"Cannot listen on UDP port {port}.");
	}

	public BattleRoom Room { get; }
	public int PlayerCount => _players.Count;
	public bool IsStarted => Room.State == BattleRoomState.Running;
	public long NextTick => _nextTick;

	public void PollEvents()
	{
		ThrowIfDisposed();
		_manager.PollEvents();
	}

	public void StartMatch(Map map)
	{
		ThrowIfDisposed();
		if (Room.State != BattleRoomState.Lobby || _players.Count == 0)
			throw new InvalidOperationException("The room cannot start.");

		var bytes = Encoding.UTF8.GetBytes(TomlSerializer.Serialize(map));
		if (bytes.Length > TurnProtocol.MaxMapBytes) throw new ArgumentOutOfRangeException(nameof(map), "Map is too large.");
		Room.State = BattleRoomState.Loading;
		var writer = new NetDataWriter();
		writer.Put(TurnProtocol.Map);
		writer.Put(bytes.Length);
		writer.Put(bytes);
		foreach (var player in _players) player.Send(writer, DeliveryMethod.ReliableOrdered);
	}

	private void OnConnected(NetPeer peer)
	{
		if (Room.State != BattleRoomState.Lobby || _players.Count >= _maxPlayers)
		{
			peer.Disconnect();
		}
	}

	private void OnDisconnected(NetPeer peer, DisconnectInfo info)
	{
		_rejected.Remove(peer);
		if (!_players.Remove(peer)) return;
		_submissions.Clear();
		_ready.Clear();

		// Assigned player IDs cannot change while clients remain connected.
		_manager.DisconnectAll();
		_players.Clear();
		_nextTick = 1;
		Room.Reset();
	}

	private void OnReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
	{
		try
		{
			if (reader.AvailableBytes < 1 || _rejected.Contains(peer)) return;
			switch (reader.GetByte())
			{
				case TurnProtocol.Join:
					ReceiveJoin(peer, reader);
					break;
				case TurnProtocol.Ready when Room.State == BattleRoomState.Loading && _players.Contains(peer) &&
				                             reader.AvailableBytes == 0:
					_ready.Add(peer);
					if (_ready.Count == _players.Count) BeginTurns();
					break;
				case TurnProtocol.Submit when IsStarted && _players.Contains(peer):
					ReceiveSubmission(peer, reader);
					break;
			}
		}
		catch(Exception exception) when(exception is IndexOutOfRangeException or ArgumentException or InvalidOperationException)
		{
			if (!_players.Contains(peer)) Reject(peer, "Invalid join packet");
		}
		finally
		{
			reader.Recycle();
		}
	}

	private void ReceiveJoin(NetPeer peer, NetPacketReader reader)
	{
		if (Room.State != BattleRoomState.Lobby || _players.Contains(peer) || reader.AvailableBytes > 256) return;
		var checksum = reader.GetString();
		var name = reader.GetString();
		if (reader.AvailableBytes != 0 || checksum != Room.ModChecksum ||
		    string.IsNullOrWhiteSpace(name) || name.Length > 32 ||
		    Room.Players.Any(player => string.Equals(player.Name, name, StringComparison.Ordinal)))
		{
			Reject(peer, checksum != Room.ModChecksum ? "Mod checksum mismatch" : "Invalid or duplicate player name");
			return;
		}

		if (_players.Count >= _maxPlayers)
		{
			Reject(peer, "Room is full");
			return;
		}

		var id = _players.Count;
		_players.Add(peer);
		Room.AddPlayer(id, name);
		var writer = new NetDataWriter();
		writer.Put(TurnProtocol.Welcome);
		writer.Put(id);
		peer.Send(writer, DeliveryMethod.ReliableOrdered);
		BroadcastRoster();
	}

	private void Reject(NetPeer peer, string reason)
	{
		_rejected.Add(peer);
		var writer = new NetDataWriter();
		writer.Put(TurnProtocol.Reject);
		writer.Put(reason);
		peer.Send(writer, DeliveryMethod.ReliableOrdered);
	}

	private void BroadcastRoster()
	{
		var writer = new NetDataWriter();
		writer.Put(TurnProtocol.Roster);
		writer.Put(Room.Players.Count);
		foreach (var player in Room.Players)
		{
			writer.Put(player.Id);
			writer.Put(player.Name);
		}
		foreach (var peer in _players) peer.Send(writer, DeliveryMethod.ReliableOrdered);
	}

	private void BeginTurns()
	{
		Room.State = BattleRoomState.Running;
		var writer = new NetDataWriter();
		writer.Put(TurnProtocol.Start);
		writer.Put(_players.Count);
		foreach (var player in _players) player.Send(writer, DeliveryMethod.ReliableOrdered);
	}

	private void ReceiveSubmission(NetPeer peer, NetPacketReader reader)
	{
		if (reader.AvailableBytes < sizeof(long) + sizeof(int)) return;
		var tick = reader.GetLong();
		var length = reader.GetInt();
		if (tick != _nextTick || _submissions.ContainsKey(peer) || length < 0 ||
		    length > TurnProtocol.MaxPayloadBytes || reader.AvailableBytes != length) return;

		var payload = new byte[length];
		reader.GetBytes(payload, 0, length);
		_submissions.Add(peer, payload);
		if (_submissions.Count == _players.Count) BroadcastFrame();
	}

	private void BroadcastFrame()
	{
		var writer = new NetDataWriter();
		writer.Put(TurnProtocol.Frame);
		writer.Put(_nextTick);
		writer.Put(_players.Count);
		for (var i = 0; i < _players.Count; i++)
		{
			var payload = _submissions[_players[i]];
			writer.Put(i);
			writer.Put(payload.Length);
			writer.Put(payload);
		}

		foreach (var player in _players) player.Send(writer, DeliveryMethod.ReliableOrdered);
		_submissions.Clear();
		_nextTick++;
	}

	public void Dispose()
	{
		if (_isDisposed) return;
		_isDisposed = true;
		_manager.Stop();
	}

	private void ThrowIfDisposed()
	{
		if (_isDisposed) throw new InvalidOperationException("Server is already disposed.");
	}
}