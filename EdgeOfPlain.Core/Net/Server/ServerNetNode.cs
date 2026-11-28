using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using EdgeOfPlain.Core.Definitions.Worlds;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Utils;
using LiteNetLib;
using LiteNetLib.Utils;
using Microsoft.Extensions.Logging;
using Tomlyn;

namespace EdgeOfPlain.Core.Net.Server
{
	public sealed partial class ServerNetNode : INetNode
	{
		private const int BadPacketWindow = 10;
		private const int BadPacketsToKick = 5;
		private static readonly byte[] _emptyPayload = [.. "\0\0"u8];

		private sealed class Slot(int id, string name, int team, RoomPlayerKind kind)
		{
			public int Id { get; } = id;
			public string Name { get; } = name;
			public int Team { get; set; } = team;
			public RoomPlayerKind Kind { get; } = kind;
			public NetPeer? Peer { get; set; }
			public bool Active { get; set; } = true;
			public Queue<bool> Recent { get; } = new(BadPacketWindow);
			public bool IsHuman => Kind == RoomPlayerKind.Human;
		}

		private readonly EventBasedNetListener _listener;
		private readonly NetManager _manager;
		private readonly List<Slot> _slots = [];
		private readonly Dictionary<NetPeer, Slot> _byPeer = [];
		private readonly Dictionary<int, byte[]> _submissions = [];
		private readonly HashSet<int> _ready = [];
		private readonly Dictionary<NetPeer, long> _pending = [];
		private readonly Dictionary<int, byte[]> _hashes = [];
		private readonly NetworkOptions _options;
		private byte[] _mapBytes = [];
		private string _mapId = "";
		private string _mapChecksum = "";
		private long _phaseStarted;
		private long _nextTurnAt;
		private long _hashTick;
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
				else if (ActivePlayerCount + _pending.Count >= Room.Options.MaxPlayers ||
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
		public int PlayerCount => ActivePlayerCount;
		public int PendingConnectionCount => _pending.Count;
		public bool IsStarted => Room.State == BattleRoomState.Running;
		public long NextTick { get; private set; } = TurnProtocol.FirstTick;

		private int ActivePlayerCount => _slots.Count(slot => slot.Active);

		public void PollEvents()
		{
			ThrowIfDisposed();
			_manager.PollEvents();
			CheckTimeouts();
			PollTurnTimer();
			PollSynchronization();
		}

		public void StartMatch(Map map)
		{
			ThrowIfDisposed();
			if (Room.State != BattleRoomState.Lobby || ActivePlayerCount == 0)
				throw new InvalidOperationException("The room cannot start.");

			var bytes = Encoding.UTF8.GetBytes(TomlSerializer.Serialize(map));
			if (bytes.Length > Room.Options.MaxMapBytes)
				throw new ArgumentOutOfRangeException(nameof(map), "Map is too large for this room.");

			_mapBytes = bytes;
			_mapChecksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
			_mapId = $"map-{_mapChecksum[..16]}";
			Room.State = BattleRoomState.Loading;
			_phaseStarted = _options.TimeProvider.GetTimestamp();
			Broadcast(TurnProtocol.MapInfo(WorldKey.Primary, _mapId, _mapChecksum));
		}

		private void OnConnected(NetPeer peer)
		{
			if (Room.State != BattleRoomState.Lobby) Reject(peer, ConnectionRejection.RoomNotJoinable);
			else if (ActivePlayerCount >= Room.Options.MaxPlayers) Reject(peer, ConnectionRejection.RoomFull);
		}

		private void OnDisconnected(NetPeer peer, DisconnectInfo info)
		{
			_pending.Remove(peer);
			if (_byPeer.TryGetValue(peer, out var slot)) RemoveSlot(slot, ConnectionRejection.ConnectionLost);
		}

		private void OnReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
		{
			try
			{
				if (peer.ConnectionState != ConnectionState.Connected) return;
				TurnProtocol.Require(channel == TurnProtocol.Channel && deliveryMethod == TurnProtocol.Delivery);
				TurnProtocol.Require(reader.AvailableBytes >= sizeof(byte));
				var message = (TurnMessage)reader.GetByte();
				var handled = true;
				switch (message)
				{
					case TurnMessage.Join:
						ReceiveJoin(peer, reader);
						break;
					case TurnMessage.Ready when Room.State == BattleRoomState.Loading && _byPeer.ContainsKey(peer):
						TurnProtocol.RequireEnd(reader);
						_ready.Add(_byPeer[peer].Id);
						TryBeginTurns();
						break;
					case TurnMessage.Submit when (IsStarted || Room.State == BattleRoomState.Synchronizing) &&
					                             _byPeer.ContainsKey(peer):
						ReceiveSubmission(peer, reader);
						break;
					case TurnMessage.MapDataRequest when Room.State == BattleRoomState.Loading &&
					                                      _byPeer.ContainsKey(peer):
						SendMapData(peer, reader);
						break;
					case TurnMessage.StateHash when (IsStarted || Room.State == BattleRoomState.Synchronizing) &&
					                                 _byPeer.ContainsKey(peer):
						ReceiveStateHash(peer, reader);
						break;
					case TurnMessage.AssignTeam when Room.State == BattleRoomState.Lobby && IsAdministrator(peer):
						ReceiveAssignTeam(peer, reader);
						break;
					case TurnMessage.AddAi when Room.State == BattleRoomState.Lobby && IsAdministrator(peer):
						ReceiveAddAi(reader);
						break;
					case TurnMessage.AiInput when (IsStarted || Room.State == BattleRoomState.Synchronizing) &&
					                              IsAdministrator(peer):
						ReceiveAiInput(reader);
						break;
					default:
						handled = ReceiveSynchronization(peer, message, reader);
						break;
				}
				if (!handled) throw new InvalidDataException("Unexpected turn protocol message.");
				RecordGood(peer);
			}
			catch (Exception exception) when (TurnProtocol.IsPacketException(exception))
			{
				RecordBad(peer);
			}
			finally
			{
				reader.Recycle();
			}
		}

		private bool IsAdministrator(NetPeer peer)
			=> _byPeer.TryGetValue(peer, out var slot) && slot.Active && Room.AdministratorId == slot.Id;

		private void ReceiveJoin(NetPeer peer, NetPacketReader reader)
		{
			if (Room.State != BattleRoomState.Lobby || _byPeer.ContainsKey(peer))
			{
				Reject(peer, ConnectionRejection.RoomNotJoinable);
				return;
			}
			var (checksum, name) = TurnProtocol.ReadJoin(reader, Room.Options);
			if (checksum != Room.ModChecksum ||
			    !BattleRoomOptions.IsValidPlayerName(name, Room.Options.MaxPlayerNameLength) ||
			    _slots.Any(slot => slot.Active && string.Equals(slot.Name, name, StringComparison.Ordinal)))
			{
				Reject(peer, checksum != Room.ModChecksum
					? ConnectionRejection.ModChecksumMismatch : ConnectionRejection.InvalidPlayerName);
				return;
			}

			if (ActivePlayerCount >= Room.Options.MaxPlayers)
			{
				Reject(peer, ConnectionRejection.RoomFull);
				return;
			}

			var id = NextSlotId();
			_pending.Remove(peer);
			var slot = new Slot(id, name, NextTeam(), RoomPlayerKind.Human) { Peer = peer };
			_slots.Add(slot);
			_byPeer.Add(peer, slot);
			Room.AdministratorId ??= id;
			peer.Send(TurnProtocol.Welcome(id, Room.Options), TurnProtocol.Channel, TurnProtocol.Delivery);
			BroadcastRoster();
		}

		private int NextSlotId()
		{
			var id = 0;
			while (_slots.Any(slot => slot.Active && slot.Id == id)) id++;
			return id;
		}

		private int NextTeam() => ActivePlayerCount % 2;

		private void SendMapData(NetPeer peer, NetPacketReader reader)
		{
			var key = TurnProtocol.ReadMapDataRequest(reader);
			TurnProtocol.Require(key == WorldKey.Primary);
			peer.Send(TurnProtocol.MapData(key, _mapBytes, Room.Options), TurnProtocol.Channel, TurnProtocol.Delivery);
		}

		private void TryBeginTurns()
		{
			var humans = _slots.Where(slot => slot is { Active: true, IsHuman: true }).ToArray();
			if (humans.Length == 0 || humans.Any(slot => !_ready.Contains(slot.Id))) return;
			BeginTurns();
		}

		private void BeginTurns()
		{
			Room.State = BattleRoomState.Running;
			_phaseStarted = _options.TimeProvider.GetTimestamp();
			_nextTurnAt = _phaseStarted;
			Broadcast(TurnProtocol.Start(ActivePlayerCount));
		}

		private void ReceiveSubmission(NetPeer peer, NetPacketReader reader)
		{
			var slot = _byPeer[peer];
			var (tick, payload) = TurnProtocol.ReadSubmission(reader, Room.Options);
			if (tick < NextTick) return;
			TurnProtocol.Require(tick == NextTick && !_submissions.ContainsKey(slot.Id));
			if (Room.State == BattleRoomState.Synchronizing) TurnProtocol.Require(CanReceivePausedSubmission(peer));
			NetworkCommands.Validate(payload);
			_submissions.Add(slot.Id, payload);
		}

		private void ReceiveAiInput(NetPacketReader reader)
		{
			var (playerId, tick, payload) = TurnProtocol.ReadAiInput(reader, Room.Options);
			if (tick < NextTick) return;
			var slot = _slots.Find(candidate => candidate.Active && candidate.Id == playerId &&
			                                    candidate.Kind == RoomPlayerKind.Ai);
			TurnProtocol.Require(slot is not null && tick == NextTick && !_submissions.ContainsKey(playerId));
			NetworkCommands.Validate(payload);
			_submissions.Add(playerId, payload);
		}

		private void ReceiveStateHash(NetPeer peer, NetPacketReader reader)
		{
			var slot = _byPeer[peer];
			var (tick, hash) = TurnProtocol.ReadStateHash(reader);
			if (tick != NextTick - 1) return;
			if (tick != _hashTick)
			{
				_hashTick = tick;
				_hashes.Clear();
			}
			if (!_hashes.TryAdd(slot.Id, hash)) return;
			if (_hashes.Count < 2) return;
			foreach (var other in _hashes.Values)
			{
				if (other.AsSpan().SequenceEqual(hash)) continue;
				ReportDesync(tick);
				return;
			}
		}

		private void ReportDesync(long tick)
		{
			LogUtils.Logger<ServerNetNode>().LogWarning("state hash mismatch at tick {Tick}", tick);
			if (_synchronization is not null || !IsStarted) return;
			try
			{
				_ = SynchronizeAsync();
			}
			catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException)
			{
				LogUtils.Logger<ServerNetNode>().LogWarning("cannot start synchronization: {Reason}", exception.Message);
			}
		}

		private void ReceiveAssignTeam(NetPeer peer, NetPacketReader reader)
		{
			var (playerId, team) = TurnProtocol.ReadAssignTeam(reader);
			TurnProtocol.Require(team < Room.Options.MaxPlayers);
			var slot = _slots.Find(candidate => candidate.Active && candidate.Id == playerId);
			TurnProtocol.Require(slot is not null);
			slot!.Team = team;
			BroadcastRoster();
		}

		private void ReceiveAddAi(NetPacketReader reader)
		{
			var name = TurnProtocol.ReadAddAi(reader, Room.Options);
			TurnProtocol.Require(!_slots.Any(slot => slot.Active &&
			                                         string.Equals(slot.Name, name, StringComparison.Ordinal)));
			TurnProtocol.Require(ActivePlayerCount < Room.Options.MaxPlayers);
			var slot = new Slot(NextSlotId(), name, NextTeam(), RoomPlayerKind.Ai);
			_slots.Add(slot);
			BroadcastRoster();
		}

		public void SetAdministrator(int playerId)
		{
			ThrowIfDisposed();
			if (Room.State != BattleRoomState.Lobby) throw new InvalidOperationException("Change the administrator in the lobby.");
			ArgumentOutOfRangeException.ThrowIfNegative(playerId);
			if (!_slots.Any(slot => slot.Active && slot.Id == playerId))
				throw new ArgumentOutOfRangeException(nameof(playerId));
			Room.AdministratorId = playerId;
			BroadcastRoster();
		}

		private void Reject(NetPeer peer, ConnectionRejection reason)
		{
			_pending.Remove(peer);
			peer.Disconnect(TurnProtocol.Reject(reason));
		}

		private void RemoveSlot(Slot slot, ConnectionRejection reason)
		{
			if (slot.Peer is { } peer)
			{
				_byPeer.Remove(peer);
				if (reason != ConnectionRejection.ConnectionLost) peer.Disconnect(TurnProtocol.Reject(reason));
				slot.Peer = null;
			}
			slot.Active = false;
			_ready.Remove(slot.Id);
			_submissions.Remove(slot.Id);
			_hashes.Remove(slot.Id);
			if (_synchronization is not null)
			{
				CloseRoom(ConnectionRejection.SynchronizationFailed,
					new InvalidOperationException("A player left during synchronization."));
				return;
			}
			Broadcast(TurnProtocol.PlayerRemoved(slot.Id, reason));
			if (Room.AdministratorId == slot.Id)
			{
				Room.AdministratorId = _slots.FirstOrDefault(candidate => candidate is { Active: true, IsHuman: true })?.Id;
			}
			BroadcastRoster();
			if (!_slots.Any(candidate => candidate is { Active: true, IsHuman: true }))
				CloseRoom(ConnectionRejection.RoomClosed, new InvalidOperationException("No players remain."));
		}

		private void RecordGood(NetPeer peer)
		{
			if (_byPeer.TryGetValue(peer, out var slot)) Record(slot, true);
		}

		private void RecordBad(NetPeer peer)
		{
			if (!_byPeer.TryGetValue(peer, out var slot))
			{
				Reject(peer, ConnectionRejection.InvalidPacket);
				return;
			}
			Record(slot, false);
			if (slot.Recent.Count(quality => !quality) >= BadPacketsToKick)
				RemoveSlot(slot, ConnectionRejection.TooManyBadPackets);
		}

		private static void Record(Slot slot, bool good)
		{
			slot.Recent.Enqueue(good);
			while (slot.Recent.Count > BadPacketWindow) slot.Recent.Dequeue();
		}

		private void BroadcastRoster()
		{
			var players = _slots
				.Where(slot => slot.Active)
				.OrderBy(slot => slot.Id)
				.Select(slot => new RoomPlayer(slot.Id, slot.Name, slot.Team, slot.Kind))
				.ToArray();
			Room.SetPlayers(players);
			Broadcast(TurnProtocol.Roster(players, Room.AdministratorId ?? -1));
		}

		private void CloseRoom(ConnectionRejection reason, Exception failure)
		{
			EndSynchronization(failure);
			foreach (var peer in _byPeer.Keys) peer.Disconnect(TurnProtocol.Reject(reason));
			foreach (var peer in _pending.Keys) peer.Disconnect(TurnProtocol.Reject(reason));
			_pending.Clear();
			_byPeer.Clear();
			_slots.Clear();
			_submissions.Clear();
			_hashes.Clear();
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
		}

		private void PollTurnTimer()
		{
			if (!IsStarted) return;
			if (_options.TimeProvider.GetElapsedTime(_nextTurnAt) < _options.TurnInterval) return;
			BroadcastFrame();
			_nextTurnAt = _options.TimeProvider.GetTimestamp();
		}

		private void BroadcastFrame()
		{
			var active = _slots.Where(slot => slot.Active).OrderBy(slot => slot.Id).ToArray();
			var commands = new PlayerCommands[active.Length];
			for (var i = 0; i < active.Length; i++)
				commands[i] = new PlayerCommands(active[i].Id, active[i].Team,
					_submissions.GetValueOrDefault(active[i].Id) ?? _emptyPayload);
			Broadcast(TurnProtocol.Frame(new TurnFrame(NextTick, commands)));
			_submissions.Clear();
			NextTick++;
			_phaseStarted = _options.TimeProvider.GetTimestamp();
		}

		private void Broadcast(NetDataWriter writer)
		{
			foreach (var slot in _slots)
				if (slot is { Active: true, Peer: { } peer })
					peer.Send(writer, TurnProtocol.Channel, TurnProtocol.Delivery);
		}

		public void Dispose()
		{
			if (_isDisposed) return;
			_isDisposed = true;
			EndSynchronization(new ObjectDisposedException(nameof(ServerNetNode)));
			_manager.Stop();
			_pending.Clear();
			_byPeer.Clear();
			_slots.Clear();
			_submissions.Clear();
			_hashes.Clear();
			_ready.Clear();
			NextTick = TurnProtocol.FirstTick;
			Room.Reset();
		}

		private void ThrowIfDisposed()
		{
			if (_isDisposed) throw new ObjectDisposedException(nameof(ServerNetNode));
		}
	}
}
