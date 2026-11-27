using System;
using System.Collections.Generic;
using System.Net;
using EdgeOfPlain.Core.Definitions.Worlds;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Game.Snapshots;
using FixedMathSharp;
using LiteNetLib;

namespace EdgeOfPlain.Core.Net.Client
{
	public sealed partial class ClientNetNode : INetNode
	{
		private readonly EventBasedNetListener _listener = new();
		private readonly NetManager _manager;
		private readonly Queue<TurnFrame> _frames = new();
		private NetPeer? _server;
		private long _nextFrameTick = TurnProtocol.FirstTick;
		private long _nextSubmitTick = TurnProtocol.FirstTick;
		private readonly string _name;
		private bool _isDisposed;
		private readonly NetworkOptions _options;
		private long _joinStarted;
		private long _loadingStarted;
		private long _submissionStarted;
		private (WorldKey Key, string MapId, string Checksum) _pendingMapInfo;

		public ClientNetNode(string host, int port, string? checksum = null, string? playerName = null,
			NetworkOptions? networkOptions = null)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(host);
			ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
			ArgumentOutOfRangeException.ThrowIfGreaterThan(port, IPEndPoint.MaxPort);
			var modChecksum = checksum ?? Globals.ModChecksum
				?? throw new InvalidOperationException("Load mods before joining a battle room.");
			_name = playerName ?? Globals.PlayerName;
			if (!BattleRoomOptions.IsValidPlayerName(_name, TurnProtocol.MaxStringCharacters))
				throw new ArgumentException("Player name is empty or too large to send.", nameof(playerName));
			Room = new BattleRoom(modChecksum);
			_options = networkOptions ?? NetworkOptions.Default;
			_listener.PeerConnectedEvent += peer =>
			{
				_server = peer;
				Status = ClientConnectionStatus.Joining;
				_joinStarted = _options.TimeProvider.GetTimestamp();
				peer.Send(TurnProtocol.Join(modChecksum, _name), TurnProtocol.Channel, TurnProtocol.Delivery);
			};
			_listener.PeerDisconnectedEvent += OnDisconnected;
			_listener.NetworkReceiveEvent += OnReceive;
			_manager = _options.CreateManager(_listener);
			try
			{
				if (!_manager.Start()) throw new InvalidOperationException("Cannot start the client network socket.");
				_manager.Connect(host, port, TurnProtocol.ConnectionKey);
			}
			catch
			{
				_manager.Stop();
				throw;
			}
		}

		public int? PlayerId { get; private set; }
		public int Team { get; private set; }
		public int PlayerCount => Room.Players.Count;
		public BattleRoom Room { get; }
		public ConnectionRejection Rejection { get; private set; }
		public ClientConnectionStatus Status { get; private set; } = ClientConnectionStatus.Connecting;
		public DisconnectReason? LastDisconnectReason { get; private set; }
		public long AppliedTick { get; private set; }
		public bool HasPendingSubmission => _nextSubmitTick > AppliedTick + 1;
		public string? RejectionReason => Rejection switch
		{
			ConnectionRejection.None => null,
			ConnectionRejection.ModChecksumMismatch => "Mod checksum mismatch",
			ConnectionRejection.InvalidPlayerName => "Invalid or duplicate player name",
			ConnectionRejection.RoomFull => "Room is full",
			ConnectionRejection.RoomNotJoinable => "The room is not accepting players",
			ConnectionRejection.JoinTimeout => "Joining the room timed out",
			ConnectionRejection.LoadingTimeout => "World loading timed out",
			ConnectionRejection.TurnTimeout => "Command submission timed out",
			ConnectionRejection.ConnectionFailed => "Could not connect to the server",
			ConnectionRejection.ConnectionLost => "The connection was lost",
			ConnectionRejection.ProtocolMismatch => "Incompatible network protocol",
			ConnectionRejection.SynchronizationTimeout => "World synchronization timed out",
			ConnectionRejection.SynchronizationFailed => "World synchronization failed",
			ConnectionRejection.RoomClosed => "The room was closed",
			ConnectionRejection.TooManyBadPackets => "Too many invalid packets were received from this client",
			_ => "Invalid server packet",
		};

		public WorldKey? PendingMapKey { get; private set; }

		public (WorldKey Key, string MapId, string Checksum)? PendingMapInfo
			=> PendingMapKey is null ? null : _pendingMapInfo;
		public IMapCache? MapCache { get; set; }
		public Map? PendingMap { get; private set; }
		public bool IsStarted => Room.State == BattleRoomState.Running;
		public int AvailableFrames => _frames.Count;

		public void RequestMapData(WorldKey key)
		{
			ThrowIfDisposed();
			if (_server is null) throw new InvalidOperationException("The client is not connected.");
			if (Room.State != BattleRoomState.Loading) throw new InvalidOperationException("The room is not loading a world.");
			if (PendingMapKey != key) throw new ArgumentOutOfRangeException(nameof(key), "This world is not waiting for a map.");
			_server.Send(TurnProtocol.MapDataRequest(key), TurnProtocol.Channel, TurnProtocol.Delivery);
		}

		public void MarkWorldReady(GameWorld world)
		{
			ThrowIfDisposed();
			ArgumentNullException.ThrowIfNull(world);
			if (Room.State != BattleRoomState.Loading || PendingMap is not { } map || _server is null)
				throw new InvalidOperationException("No map is waiting for world creation.");
			if (world.Key != PendingMapKey)
				throw new ArgumentException("World key does not match the requested map.", nameof(world));
			if (world.Map.Width != map.Width || world.Map.Height != map.Height)
				throw new ArgumentException("World dimensions do not match the received map.", nameof(world));
			for (var y = 0; y < map.Height; y++)
			for (var x = 0; x < map.Width; x++)
				if (world.Map.GetTileId(x, y) != map.Tiles[x, y])
					throw new ArgumentException("World tiles do not match the received map.", nameof(world));

			PendingMapKey = null;
			_pendingMapInfo = default;
			PendingMap = null;
			_server.Send(TurnProtocol.Create(TurnMessage.Ready), TurnProtocol.Channel, TurnProtocol.Delivery);
		}

		public void RequestAssignTeam(int playerId, int team)
		{
			ThrowIfDisposed();
			if (_server is null || Room.State != BattleRoomState.Lobby)
				throw new InvalidOperationException("Teams can only be assigned in the lobby.");
			if (Room.AdministratorId != PlayerId)
				throw new InvalidOperationException("Only the administrator can assign teams.");
			ArgumentOutOfRangeException.ThrowIfNegative(playerId);
			ArgumentOutOfRangeException.ThrowIfNegative(team);
			ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(team, Room.Options.MaxPlayers);
			_server.Send(TurnProtocol.AssignTeam(playerId, team), TurnProtocol.Channel, TurnProtocol.Delivery);
		}

		public void RequestAddAi(string name)
		{
			ThrowIfDisposed();
			if (_server is null || Room.State != BattleRoomState.Lobby)
				throw new InvalidOperationException("AI players can only be added in the lobby.");
			if (Room.AdministratorId != PlayerId)
				throw new InvalidOperationException("Only the administrator can add AI players.");
			if (!BattleRoomOptions.IsValidPlayerName(name, Room.Options.MaxPlayerNameLength))
				throw new ArgumentException("AI name is empty or too large to send.", nameof(name));
			_server.Send(TurnProtocol.AddAi(name), TurnProtocol.Channel, TurnProtocol.Delivery);
		}

		public void PollEvents()
		{
			ThrowIfDisposed();
			_manager.PollEvents();
			if (Status == ClientConnectionStatus.Joining &&
			    _options.TimeProvider.GetElapsedTime(_joinStarted) >= _options.JoinTimeout)
				FailConnection(ConnectionRejection.JoinTimeout);
			else if (Room.State == BattleRoomState.Loading &&
			         _options.TimeProvider.GetElapsedTime(_loadingStarted) >= _options.LoadingTimeout)
				FailConnection(ConnectionRejection.LoadingTimeout);
			PollSynchronization();
		}

		public void Submit(long tick, ReadOnlySpan<byte> payload)
		{
			ThrowIfDisposed();
			if (!IsStarted || _server is null) throw new InvalidOperationException("The match has not started.");
			if (tick < _nextSubmitTick)
				throw new ArgumentOutOfRangeException(nameof(tick), "Submit each tick at most once, in order.");
			if (tick != _nextFrameTick)
				throw new InvalidOperationException("Wait for the preceding frame before submitting the next tick.");
			if (payload.Length > Room.Options.MaxPayloadBytes) throw new ArgumentOutOfRangeException(nameof(payload));
			var bytes = payload.ToArray();
			NetworkCommands.Validate(bytes);

			_server.Send(TurnProtocol.Submit(tick, bytes), TurnProtocol.Channel, TurnProtocol.Delivery);
			_nextSubmitTick = tick + 1;
		}

		public void Submit(long tick, NetworkCommandInput input)
		{
			ThrowIfDisposed();
			ArgumentNullException.ThrowIfNull(input);
			if (!IsStarted || tick != _nextFrameTick || tick < _nextSubmitTick)
				throw new InvalidOperationException("The match is not ready for this tick.");
			Submit(tick, input.Flush(Room.Options.MaxPayloadBytes));
		}

		public void SubmitAi(int playerId, long tick, NetworkCommandInput input)
		{
			ThrowIfDisposed();
			ArgumentNullException.ThrowIfNull(input);
			if (!IsStarted || Room.AdministratorId != PlayerId)
				throw new InvalidOperationException("Only the administrator can submit AI commands.");
			if (tick != _nextFrameTick)
				throw new InvalidOperationException("Wait for the preceding frame before submitting the next tick.");
			SubmitAi(playerId, tick, input.Flush(Room.Options.MaxPayloadBytes));
		}

		public void SubmitAi(int playerId, long tick, byte[] payload)
		{
			ThrowIfDisposed();
			ArgumentNullException.ThrowIfNull(payload);
			if (!IsStarted || _server is null || Room.AdministratorId != PlayerId)
				throw new InvalidOperationException("Only the administrator can submit AI commands.");
			if (tick != _nextFrameTick)
				throw new InvalidOperationException("Wait for the preceding frame before submitting the next tick.");
			if (payload.Length > Room.Options.MaxPayloadBytes) throw new ArgumentOutOfRangeException(nameof(payload));
			NetworkCommands.Validate(payload);
			_server.Send(TurnProtocol.AiInput(playerId, tick, payload), TurnProtocol.Channel, TurnProtocol.Delivery);
		}

		public bool TryDequeueFrame(out TurnFrame frame)
		{
			ThrowIfDisposed();
			return _frames.TryDequeue(out frame);
		}

		public bool TryEnqueueCommands(long expectedTick, ICommandSink sink)
		{
			ThrowIfDisposed();
			ArgumentNullException.ThrowIfNull(sink);
			if (!_frames.TryPeek(out var frame)) return false;
			if (frame.Tick != expectedTick) throw new InvalidOperationException("Unexpected command frame tick.");
			try
			{
				NetworkCommands.Enqueue(frame, sink);
			}
			catch(Exception exception) when(TurnProtocol.IsPacketException(exception))
			{
				FailConnection(ConnectionRejection.InvalidPacket);
				return false;
			}
			_frames.Dequeue();
			return true;
		}

		public bool TryStepWorld(long expectedTick, GameWorld world, Fixed64 delta)
		{
			ArgumentNullException.ThrowIfNull(world);
			if (!IsStarted || !TryEnqueueCommands(expectedTick, world.Commands)) return false;
			world.Step(delta);
			AppliedTick = expectedTick;
			return true;
		}

		public bool TryStepWorld(long expectedTick, GameCore core, Fixed64 delta)
		{
			ArgumentNullException.ThrowIfNull(core);
			if (!IsStarted || !TryEnqueueCommands(expectedTick, core)) return false;
			core.Step(delta);
			AppliedTick = expectedTick;
			if (_server is not null && _boundCore is not null &&
			    AppliedTick % _options.StateHashInterval == 0)
			{
				var hash = GameSnapshot.ComputeStateHash(core);
				_server.Send(TurnProtocol.StateHash(AppliedTick, hash), TurnProtocol.Channel, TurnProtocol.Delivery);
			}
			return true;
		}

		private void OnReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
		{
			try
			{
				if (!ReferenceEquals(peer, _server)) return;
				TurnProtocol.Require(channel == TurnProtocol.Channel && deliveryMethod == TurnProtocol.Delivery);
				TurnProtocol.Require(reader.AvailableBytes >= sizeof(byte));
				var message = (TurnMessage)reader.GetByte();
				switch (message)
				{
					case TurnMessage.Reject:
						Rejection = TurnProtocol.ReadRejection(reader);
						_server.Disconnect();
						break;
					case TurnMessage.Welcome when PlayerId is null && Room.State == BattleRoomState.Lobby:
						var welcome = TurnProtocol.ReadWelcome(reader);
						TurnProtocol.Require(BattleRoomOptions.IsValidPlayerName(_name, welcome.Options.MaxPlayerNameLength));
						PlayerId = welcome.PlayerId;
						Room.Options = welcome.Options;
						Status = ClientConnectionStatus.Connected;
						break;
					case TurnMessage.Roster when PlayerId is not null:
						var roster = TurnProtocol.ReadRoster(reader, Room.Options);
						var me = Array.FindIndex(roster.Players, player => player.Id == PlayerId.Value);
						TurnProtocol.Require(me >= 0 && roster.Players[me].Name == _name);
						Team = roster.Players[me].Team;
						Room.SetPlayers(roster.Players);
						Room.AdministratorId = roster.AdministratorId;
						break;
					case TurnMessage.MapInfo when Room.State == BattleRoomState.Lobby && PlayerId is not null:
						var info = TurnProtocol.ReadMapInfo(reader);
						PendingMapKey = info.Key;
						_pendingMapInfo = info;
						PendingMap = null;
						Room.State = BattleRoomState.Loading;
						_loadingStarted = _options.TimeProvider.GetTimestamp();
						if (MapCache is { } cache && cache.TryGet(info.Key, info.MapId, info.Checksum, out var cached))
						{
							PendingMap = cached;
						}
						else
						{
							_server!.Send(TurnProtocol.MapDataRequest(info.Key), TurnProtocol.Channel, TurnProtocol.Delivery);
						}
						break;
					case TurnMessage.MapData when Room.State == BattleRoomState.Loading && PendingMapKey is { } key:
						var data = TurnProtocol.ReadMapData(reader, Room.Options);
						TurnProtocol.Require(data.Key == key);
						PendingMap = data.Map;
						break;
					case TurnMessage.Start when PlayerId is not null &&
					                            Room.State == BattleRoomState.Loading && PendingMapKey is null:
						var count = TurnProtocol.ReadStart(reader, Room.Options);
						TurnProtocol.Require(count == Room.Players.Count);
						Room.State = BattleRoomState.Running;
						break;
					case TurnMessage.Frame when IsStarted:
						var frame = TurnProtocol.ReadFrame(reader, Room.Options);
						TurnProtocol.Require(frame.Tick == _nextFrameTick);
						foreach (var player in frame.Players) NetworkCommands.Validate(player.Payload);
						_frames.Enqueue(frame);
						_nextFrameTick++;
						break;
					case TurnMessage.PlayerRemoved when PlayerId is not null:
						var removed = TurnProtocol.ReadPlayerRemoved(reader);
						Room.RemovePlayer(removed.PlayerId);
						break;
					default:
						TurnProtocol.Require(ReceiveSynchronization(message, reader));
						break;
				}
			}
			catch(Exception exception) when(TurnProtocol.IsPacketException(exception))
			{
				FailConnection(ConnectionRejection.InvalidPacket);
			}
			finally
			{
				reader.Recycle();
			}
		}

		private void OnDisconnected(NetPeer peer, DisconnectInfo info)
		{
			LastDisconnectReason = info.Reason;
			try
			{
				var data = info.AdditionalData;
				if (data is not null && data.AvailableBytes > 0)
				{
					TurnProtocol.Require((TurnMessage)data.GetByte() == TurnMessage.Reject);
					Rejection = TurnProtocol.ReadRejection(data);
				}
			}
			catch(Exception exception) when(TurnProtocol.IsPacketException(exception))
			{
				Rejection = ConnectionRejection.InvalidPacket;
			}
			if (Rejection == ConnectionRejection.None)
				Rejection = info.Reason switch
				{
					DisconnectReason.ConnectionFailed => ConnectionRejection.ConnectionFailed,
					DisconnectReason.ConnectionRejected => ConnectionRejection.ProtocolMismatch,
					DisconnectReason.DisconnectPeerCalled => ConnectionRejection.None,
					_ => ConnectionRejection.ConnectionLost,
				};
			ResetConnection();
		}

		private void FailConnection(ConnectionRejection reason)
		{
			Rejection = reason;
			_server?.Disconnect();
			ResetConnection();
		}

		private void ResetConnection()
		{
			CancelSynchronization();
			_boundCore?.Pause();
			Status = _isDisposed ? ClientConnectionStatus.Disposed : ClientConnectionStatus.Disconnected;
			_server = null;
			PlayerId = null;
			Team = 0;
			Room.Reset();
			PendingMapKey = null;
			_pendingMapInfo = default;
			PendingMap = null;
			_frames.Clear();
			_nextFrameTick = TurnProtocol.FirstTick;
			_nextSubmitTick = TurnProtocol.FirstTick;
			AppliedTick = 0;
		}

		public void Dispose()
		{
			if (_isDisposed) return;
			_isDisposed = true;
			_manager.Stop();
			ResetConnection();
		}

		private void ThrowIfDisposed()
		{
			if (_isDisposed) throw new ObjectDisposedException(nameof(ClientNetNode));
		}
	}
}
