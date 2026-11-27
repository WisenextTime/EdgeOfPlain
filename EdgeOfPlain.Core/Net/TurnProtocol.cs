using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using EdgeOfPlain.Core.Definitions.Worlds;
using EdgeOfPlain.Core.Game;
using LiteNetLib;
using LiteNetLib.Utils;
using Tomlyn;

namespace EdgeOfPlain.Core.Net
{
	internal enum TurnMessage : byte
	{
		Welcome = 1,
		Start = 2,
		Submit = 3,
		Frame = 4,
		Join = 5,
		Reject = 6,
		Roster = 7,
		Ready = 8,
		Pause = 9,
		Paused = 10,
		RequestSnapshot = 11,
		SnapshotBegin = 12,
		SnapshotChunk = 13,
		SnapshotApplied = 14,
		Resume = 15,
		SynchronizationFailed = 16,
		MapInfo = 17,
		MapDataRequest = 18,
		MapData = 19,
		StateHash = 20,
		AssignTeam = 21,
		AddAi = 22,
		PlayerRemoved = 23,
		AiInput = 24,
	}

	internal static class TurnProtocol
	{
		public const int Version = 4;
		public static readonly string ConnectionKey = $"EdgeOfPlain.Turns.{Version}";
		public const long FirstTick = 1;
		public const byte Channel = 0;
		public const DeliveryMethod Delivery = DeliveryMethod.ReliableOrdered;
		public const int FrameHeaderBytes = sizeof(byte) + sizeof(long) + sizeof(int);
		public const int MaxStringCharacters = (ushort.MaxValue - 1) / 3;
		public const int HashBytes = SHA256.HashSizeInBytes;
		private const int ChecksumCharacters = SHA256.HashSizeInBytes * 2;

		public static NetDataWriter Create(TurnMessage message)
		{
			var writer = new NetDataWriter();
			writer.Put((byte)message);
			return writer;
		}

		public static NetDataWriter Join(string checksum, string name)
		{
			var writer = Create(TurnMessage.Join);
			writer.Put(checksum);
			writer.Put(name);
			return writer;
		}

		public static (string Checksum, string Name) ReadJoin(NetDataReader reader, BattleRoomOptions options)
		{
			var checksum = ReadString(reader, ChecksumCharacters);
			var name = ReadString(reader, options.MaxPlayerNameLength);
			RequireEnd(reader);
			return (checksum, name);
		}

		public static void ValidateChecksum(string checksum)
		{
			if (string.IsNullOrWhiteSpace(checksum) || checksum.Length > ChecksumCharacters)
				throw new ArgumentException($"Checksum must contain 1-{ChecksumCharacters} characters.", nameof(checksum));
		}

		public static NetDataWriter Welcome(int playerId, BattleRoomOptions options)
		{
			var writer = Create(TurnMessage.Welcome);
			writer.Put(playerId);
			writer.Put(options.MaxPlayers);
			writer.Put(options.MaxPayloadBytes);
			writer.Put(options.MaxMapBytes);
			writer.Put(options.MaxPlayerNameLength);
			return writer;
		}

		public static (int PlayerId, BattleRoomOptions Options) ReadWelcome(NetDataReader reader)
		{
			Require(reader.AvailableBytes == 5 * sizeof(int));
			var id = reader.GetInt();
			var options = new BattleRoomOptions
			{
				MaxPlayers = reader.GetInt(),
				MaxPayloadBytes = reader.GetInt(),
				MaxMapBytes = reader.GetInt(),
				MaxPlayerNameLength = reader.GetInt(),
			};
			options.Validate();
			Require(id >= 0 && id < options.MaxPlayers);
			return (id, options);
		}

		public static NetDataWriter Reject(ConnectionRejection reason)
		{
			var writer = Create(TurnMessage.Reject);
			writer.Put((byte)reason);
			return writer;
		}

		public static ConnectionRejection ReadRejection(NetDataReader reader)
		{
			Require(reader.AvailableBytes == sizeof(byte));
			var reason = (ConnectionRejection)reader.GetByte();
			Require(reason != ConnectionRejection.None && Enum.IsDefined(reason));
			return reason;
		}

		public static NetDataWriter Roster(IReadOnlyList<RoomPlayer> players, int administratorId)
		{
			var writer = Create(TurnMessage.Roster);
			writer.Put(administratorId);
			writer.Put(players.Count);
			foreach (var player in players)
			{
				writer.Put(player.Id);
				writer.Put(player.Name);
				writer.Put(player.Team);
				writer.Put((byte)player.Kind);
			}
			return writer;
		}

		public static (int AdministratorId, RoomPlayer[] Players) ReadRoster(NetDataReader reader, BattleRoomOptions options)
		{
			Require(reader.AvailableBytes >= sizeof(int));
			var administrator = reader.GetInt();
			var count = ReadPlayerCount(reader, options);
			Require(administrator >= -1 && administrator < options.MaxPlayers);
			Require(count <= reader.AvailableBytes / (2 * sizeof(int) + sizeof(ushort) + sizeof(byte)));
			var players = new RoomPlayer[count];
			var ids = new HashSet<int>();
			var names = new HashSet<string>(StringComparer.Ordinal);
			for (var i = 0; i < count; i++)
			{
				Require(reader.AvailableBytes >= 2 * sizeof(int) + sizeof(byte));
				var id = reader.GetInt();
				var name = ReadString(reader, options.MaxPlayerNameLength);
				var team = reader.GetInt();
				var kind = (RoomPlayerKind)reader.GetByte();
				Require(id >= 0 && id < options.MaxPlayers && ids.Add(id));
				Require(kind is RoomPlayerKind.Human or RoomPlayerKind.Ai);
				Require(team >= 0);
				Require(BattleRoomOptions.IsValidPlayerName(name, options.MaxPlayerNameLength) && names.Add(name));
				players[i] = new RoomPlayer(id, name, team, kind);
			}
			RequireEnd(reader);
			return (administrator, players);
		}

		public static NetDataWriter MapInfo(WorldKey key, string mapId, string checksum)
		{
			var writer = Create(TurnMessage.MapInfo);
			writer.Put(key.Value);
			writer.Put(mapId);
			writer.Put(checksum);
			return writer;
		}

		public static (WorldKey Key, string MapId, string Checksum) ReadMapInfo(NetDataReader reader)
		{
			var key = new WorldKey(reader.GetInt());
			var mapId = ReadString(reader, MaxStringCharacters);
			var checksum = ReadString(reader, ChecksumCharacters);
			Require(key.IsValid && mapId.Length > 0 && checksum.Length > 0);
			RequireEnd(reader);
			return (key, mapId, checksum);
		}

		public static NetDataWriter MapDataRequest(WorldKey key)
		{
			var writer = Create(TurnMessage.MapDataRequest);
			writer.Put(key.Value);
			return writer;
		}

		public static WorldKey ReadMapDataRequest(NetDataReader reader)
		{
			Require(reader.AvailableBytes == sizeof(int));
			var key = new WorldKey(reader.GetInt());
			Require(key.IsValid);
			return key;
		}

		public static NetDataWriter MapData(WorldKey key, ReadOnlySpan<byte> bytes, BattleRoomOptions options)
		{
			if (bytes.Length > options.MaxMapBytes)
				throw new ArgumentOutOfRangeException(nameof(bytes), "Map is too large for this room.");
			var writer = Create(TurnMessage.MapData);
			writer.Put(key.Value);
			WritePayload(writer, bytes);
			return writer;
		}

		public static (WorldKey Key, Map Map) ReadMapData(NetDataReader reader, BattleRoomOptions options)
		{
			var key = new WorldKey(reader.GetInt());
			Require(key.IsValid);
			var bytes = ReadPayload(reader, options.MaxMapBytes);
			Require(bytes.Length > 0);
			RequireEnd(reader);
			return (key, TomlSerializer.Deserialize<Map>(Encoding.UTF8.GetString(bytes)));
		}

		public static NetDataWriter Start(int count)
		{
			var writer = Create(TurnMessage.Start);
			writer.Put(count);
			return writer;
		}

		public static int ReadStart(NetDataReader reader, BattleRoomOptions options)
		{
			var count = ReadPlayerCount(reader, options);
			RequireEnd(reader);
			return count;
		}

		public static NetDataWriter Submit(long tick, ReadOnlySpan<byte> payload)
		{
			var writer = Create(TurnMessage.Submit);
			writer.Put(tick);
			WritePayload(writer, payload);
			return writer;
		}

		public static (long Tick, byte[] Payload) ReadSubmission(NetDataReader reader, BattleRoomOptions options)
		{
			Require(reader.AvailableBytes >= sizeof(long));
			var tick = reader.GetLong();
			var payload = ReadPayload(reader, options.MaxPayloadBytes);
			RequireEnd(reader);
			return (tick, payload);
		}

		public static NetDataWriter Frame(TurnFrame frame)
		{
			var writer = Create(TurnMessage.Frame);
			writer.Put(frame.Tick);
			writer.Put(frame.Players.Count);
			foreach (var player in frame.Players)
			{
				writer.Put(player.PlayerId);
				writer.Put(player.Team);
				WritePayload(writer, player.Payload);
			}
			return writer;
		}

		public static TurnFrame ReadFrame(NetDataReader reader, BattleRoomOptions options)
		{
			Require(reader.AvailableBytes >= sizeof(long));
			var tick = reader.GetLong();
			var count = ReadPlayerCount(reader, options);
			Require(count <= reader.AvailableBytes / (2 * sizeof(int) + sizeof(int)));
			var players = new PlayerCommands[count];
			var previous = -1;
			for (var i = 0; i < count; i++)
			{
				Require(reader.AvailableBytes >= 2 * sizeof(int));
				var id = reader.GetInt();
				var team = reader.GetInt();
				Require(id > previous && id < options.MaxPlayers && team >= 0);
				previous = id;
				players[i] = new PlayerCommands(id, team, ReadPayload(reader, options.MaxPayloadBytes));
			}
			RequireEnd(reader);
			return new TurnFrame(tick, players);
		}

		public static NetDataWriter StateHash(long tick, ReadOnlySpan<byte> hash)
		{
			Require(hash.Length == HashBytes);
			var writer = Create(TurnMessage.StateHash);
			writer.Put(tick);
			writer.Put((byte)hash.Length);
			writer.Put(hash);
			return writer;
		}

		public static (long Tick, byte[] Hash) ReadStateHash(NetDataReader reader)
		{
			Require(reader.AvailableBytes == sizeof(long) + sizeof(byte) + HashBytes);
			var tick = reader.GetLong();
			var length = reader.GetByte();
			Require(length == HashBytes);
			var hash = new byte[HashBytes];
			reader.GetBytes(hash, 0, HashBytes);
			return (tick, hash);
		}

		public static NetDataWriter AssignTeam(int playerId, int team)
		{
			var writer = Create(TurnMessage.AssignTeam);
			writer.Put(playerId);
			writer.Put(team);
			return writer;
		}

		public static (int PlayerId, int Team) ReadAssignTeam(NetDataReader reader)
		{
			Require(reader.AvailableBytes == 2 * sizeof(int));
			var playerId = reader.GetInt();
			var team = reader.GetInt();
			Require(playerId >= 0 && team >= 0);
			return (playerId, team);
		}

		public static NetDataWriter AddAi(string name)
		{
			var writer = Create(TurnMessage.AddAi);
			writer.Put(name);
			return writer;
		}

		public static string ReadAddAi(NetDataReader reader, BattleRoomOptions options)
		{
			var name = ReadString(reader, options.MaxPlayerNameLength);
			Require(BattleRoomOptions.IsValidPlayerName(name, options.MaxPlayerNameLength));
			RequireEnd(reader);
			return name;
		}

		public static NetDataWriter PlayerRemoved(int playerId, ConnectionRejection reason)
		{
			var writer = Create(TurnMessage.PlayerRemoved);
			writer.Put(playerId);
			writer.Put((byte)reason);
			return writer;
		}

		public static (int PlayerId, ConnectionRejection Reason) ReadPlayerRemoved(NetDataReader reader)
		{
			Require(reader.AvailableBytes == sizeof(int) + sizeof(byte));
			var playerId = reader.GetInt();
			var reason = (ConnectionRejection)reader.GetByte();
			Require(playerId >= 0 && reason != ConnectionRejection.None && Enum.IsDefined(reason));
			return (playerId, reason);
		}

		public static NetDataWriter AiInput(int playerId, long tick, ReadOnlySpan<byte> payload)
		{
			var writer = Create(TurnMessage.AiInput);
			writer.Put(playerId);
			writer.Put(tick);
			WritePayload(writer, payload);
			return writer;
		}

		public static (int PlayerId, long Tick, byte[] Payload) ReadAiInput(NetDataReader reader, BattleRoomOptions options)
		{
			Require(reader.AvailableBytes >= sizeof(int) + sizeof(long) + sizeof(ushort));
			var playerId = reader.GetInt();
			var tick = reader.GetLong();
			var payload = ReadPayload(reader, options.MaxPayloadBytes);
			Require(playerId >= 0 && playerId < options.MaxPlayers);
			Require(tick >= FirstTick);
			RequireEnd(reader);
			return (playerId, tick, payload);
		}

		public static void RequireEnd(NetDataReader reader) => Require(reader.AvailableBytes == 0);

		public static void Require(bool condition)
		{
			if (!condition) throw new InvalidDataException("Invalid turn protocol packet.");
		}

		public static bool IsPacketException(Exception exception)
			=> exception is InvalidDataException or IndexOutOfRangeException or ArgumentException
				or InvalidOperationException or FormatException or OverflowException;

		private static int ReadPlayerCount(NetDataReader reader, BattleRoomOptions options)
		{
			Require(reader.AvailableBytes >= sizeof(int));
			var count = reader.GetInt();
			Require(count >= 1 && count <= options.MaxPlayers);
			return count;
		}

		private static string ReadString(NetDataReader reader, int maxCharacters)
		{
			Require(reader.AvailableBytes >= sizeof(ushort));
			return reader.GetString(maxCharacters);
		}

		internal static void WritePayload(NetDataWriter writer, ReadOnlySpan<byte> bytes)
		{
			writer.Put(bytes.Length);
			writer.Put(bytes);
		}

		internal static byte[] ReadPayload(NetDataReader reader, int maxBytes)
		{
			Require(reader.AvailableBytes >= sizeof(int));
			var length = reader.GetInt();
			Require(length >= 0 && length <= maxBytes && length <= reader.AvailableBytes);
			var bytes = new byte[length];
			reader.GetBytes(bytes, 0, length);
			return bytes;
		}
	}
}
