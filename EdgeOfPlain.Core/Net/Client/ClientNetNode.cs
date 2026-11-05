using System;
using System.Collections.Generic;
using System.Text;
using EdgeOfPlain.Core.Definitions.Worlds;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Game.Commands;
using FixedMathSharp;
using LiteNetLib;
using LiteNetLib.Utils;
using Tomlyn;

namespace EdgeOfPlain.Core.Net.Client;

public sealed class ClientNetNode : INetNode
{
	private readonly EventBasedNetListener _listener = new();
	private readonly NetManager _manager;
	private readonly Queue<TurnFrame> _frames = new();
	private NetPeer? _server;
	private long _nextFrameTick = 1;
	private long _nextSubmitTick = 1;
	private readonly string _name;
	private bool _isDisposed;

	public ClientNetNode(string host, int port, string? checksum = null, string? playerName = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(host);
		var modChecksum = checksum ?? Globals.ModChecksum
			?? throw new InvalidOperationException("Load mods before joining a battle room.");
		_name = playerName ?? Globals.PlayerName;
		if (string.IsNullOrWhiteSpace(_name) || _name.Length > 32)
			throw new ArgumentException("Player name must be 1-32 characters.", nameof(playerName));
		Room = new BattleRoom(modChecksum);
		_listener.PeerConnectedEvent += peer =>
		{
			_server = peer;
			var writer = new NetDataWriter();
			writer.Put(TurnProtocol.Join);
			writer.Put(modChecksum);
			writer.Put(_name);
			peer.Send(writer, DeliveryMethod.ReliableOrdered);
		};
		_listener.PeerDisconnectedEvent += (_, _) =>
		{
			_server = null;
			PlayerId = null;
			PlayerCount = 0;
			Room.Reset();
			PendingMap = null;
			_frames.Clear();
		};
		_listener.NetworkReceiveEvent += OnReceive;
		_manager = new NetManager(_listener);
		if (!_manager.Start()) throw new InvalidOperationException("Cannot start the client network socket.");
		_manager.Connect(host, port, TurnProtocol.ConnectionKey);
	}

	public int? PlayerId { get; private set; }
	public int PlayerCount { get; private set; }
	public BattleRoom Room { get; }
	public string? RejectionReason { get; private set; }
	public Map? PendingMap { get; private set; }
	public bool IsStarted => Room.State == BattleRoomState.Running;
	public int AvailableFrames => _frames.Count;

	public void MarkWorldReady(GameWorld world)
	{
		ThrowIfDisposed();
		ArgumentNullException.ThrowIfNull(world);
		if (Room.State != BattleRoomState.Loading || PendingMap is not { } map || _server is null)
			throw new InvalidOperationException("No map is waiting for world creation.");
		if (world.Map.Width != map.Width || world.Map.Height != map.Height)
			throw new ArgumentException("World dimensions do not match the received map.", nameof(world));
		for (var y = 0; y < map.Height; y++)
		for (var x = 0; x < map.Width; x++)
			if (world.Map.GetTileId(x, y) != map.Tiles[x, y])
				throw new ArgumentException("World tiles do not match the received map.", nameof(world));

		PendingMap = null;
		var writer = new NetDataWriter();
		writer.Put(TurnProtocol.Ready);
		_server.Send(writer, DeliveryMethod.ReliableOrdered);
	}

	public void PollEvents()
	{
		ThrowIfDisposed();
		_manager.PollEvents();
	}

	public void Submit(long tick, ReadOnlySpan<byte> payload)
	{
		ThrowIfDisposed();
		if (!IsStarted || _server is null) throw new InvalidOperationException("The match has not started.");
		if (tick != _nextSubmitTick)
			throw new ArgumentOutOfRangeException(nameof(tick), "Submit each tick exactly once, in order.");
		if (tick != _nextFrameTick)
			throw new InvalidOperationException("Wait for the preceding frame before submitting the next tick.");
		if (payload.Length > TurnProtocol.MaxPayloadBytes) throw new ArgumentOutOfRangeException(nameof(payload));

		var writer = new NetDataWriter();
		writer.Put(TurnProtocol.Submit);
		writer.Put(tick);
		writer.Put(payload.Length);
		writer.Put(payload.ToArray());
		_server.Send(writer, DeliveryMethod.ReliableOrdered);
		_nextSubmitTick++;
	}

	public void Submit(long tick, NetworkCommandInput input)
	{
		ArgumentNullException.ThrowIfNull(input);
		if (!IsStarted || tick != _nextSubmitTick || tick != _nextFrameTick)
			throw new InvalidOperationException("The match is not ready for this tick.");
		Submit(tick, input.Flush());
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
		NetworkCommands.Enqueue(frame, sink);
		_frames.Dequeue();
		return true;
	}

	public bool TryStepWorld(long expectedTick, GameWorld world, Fixed64 delta)
	{
		ArgumentNullException.ThrowIfNull(world);
		if (!IsStarted || !TryEnqueueCommands(expectedTick, world.Commands)) return false;
		world.Step(delta);
		return true;
	}

	public bool TryStepWorld(long expectedTick, GameCore core, Fixed64 delta)
	{
		ArgumentNullException.ThrowIfNull(core);
		if (!IsStarted || !TryEnqueueCommands(expectedTick, core)) return false;
		core.Step(delta);
		return true;
	}

	private void OnReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
	{
		try
		{
			if (!ReferenceEquals(peer, _server) || reader.AvailableBytes < 1) return;
			switch (reader.GetByte())
			{
				case TurnProtocol.Reject:
					if (reader.AvailableBytes > 128) break;
					RejectionReason = reader.GetString();
					_server.Disconnect();
					break;
				case TurnProtocol.Welcome when reader.AvailableBytes == sizeof(int) && !IsStarted:
					var id = reader.GetInt();
					if (id is >= 0 and < TurnProtocol.MaxPlayers) PlayerId = id;
					break;
				case TurnProtocol.Roster when Room.State == BattleRoomState.Lobby && PlayerId is not null:
					ReadRoster(reader);
					break;
				case TurnProtocol.Map when Room.State == BattleRoomState.Lobby && PlayerId is not null:
					ReadMap(reader);
					break;
				case TurnProtocol.Start when reader.AvailableBytes == sizeof(int) && PlayerId is not null &&
				                             Room.State == BattleRoomState.Loading && PendingMap is null:
					var count = reader.GetInt();
					if (count <= PlayerId || count > TurnProtocol.MaxPlayers) break;
					PlayerCount = count;
					Room.State = BattleRoomState.Running;
					break;
				case TurnProtocol.Frame when IsStarted:
					ReadFrame(reader);
					break;
			}
		}
		catch(Exception exception) when(exception is IndexOutOfRangeException or ArgumentException or InvalidOperationException)
		{
			RejectionReason = "Invalid server packet";
			_server?.Disconnect();
		}
		finally
		{
			reader.Recycle();
		}
	}

	private void ReadRoster(NetPacketReader reader)
	{
		if (reader.AvailableBytes < sizeof(int) || reader.AvailableBytes > 512) return;
		var count = reader.GetInt();
		if (count < 1 || count > TurnProtocol.MaxPlayers) return;
		var players = new RoomPlayer[count];
		for (var i = 0; i < count; i++)
		{
			if (reader.AvailableBytes < sizeof(int) + 2) return;
			var id = reader.GetInt();
			if (id != i) return;
			players[i] = new RoomPlayer(id, reader.GetString());
		}
		if (reader.AvailableBytes == 0) Room.SetPlayers(players);
	}

	private void ReadMap(NetPacketReader reader)
	{
		if (reader.AvailableBytes < sizeof(int)) return;
		var length = reader.GetInt();
		if (length <= 0 || length > TurnProtocol.MaxMapBytes || reader.AvailableBytes != length) return;
		var bytes = new byte[length];
		reader.GetBytes(bytes, 0, length);
		PendingMap = TomlSerializer.Deserialize<Map>(Encoding.UTF8.GetString(bytes));
		Room.State = BattleRoomState.Loading;
	}

	private void ReadFrame(NetPacketReader reader)
	{
		if (reader.AvailableBytes < sizeof(long) + sizeof(int)) return;
		var tick = reader.GetLong();
		var count = reader.GetInt();
		if (tick != _nextFrameTick || count != PlayerCount) return;

		var players = new PlayerCommands[count];
		for (var i = 0; i < count; i++)
		{
			if (reader.AvailableBytes < 2 * sizeof(int)) return;
			var id = reader.GetInt();
			var length = reader.GetInt();
			if (id != i || length < 0 || length > TurnProtocol.MaxPayloadBytes || reader.AvailableBytes < length) return;
			var payload = new byte[length];
			reader.GetBytes(payload, 0, length);
			players[i] = new PlayerCommands(id, payload);
		}

		if (reader.AvailableBytes != 0) return;
		_frames.Enqueue(new TurnFrame(tick, players));
		_nextFrameTick++;
	}

	public void Dispose()
	{
		if (_isDisposed) return;
		_isDisposed = true;
		_manager.Stop();
	}

	private void ThrowIfDisposed()
	{
		if (_isDisposed) throw new ObjectDisposedException(nameof(ClientNetNode));
	}
}