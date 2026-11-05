using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using EdgeOfPlain.Core.Definitions.Worlds;
using LiteNetLib;
using LiteNetLib.Utils;
using Tomlyn;

namespace EdgeOfPlain.Core.Net;

internal enum TurnMessage : byte
{
	Welcome = 1,
	Start = 2,
	Submit = 3,
	Frame = 4,
	Join = 5,
	Reject = 6,
	Roster = 7,
	Map = 8,
	Ready = 9,
	Pause = 10,
	Paused = 11,
	RequestSnapshot = 12,
	SnapshotBegin = 13,
	SnapshotChunk = 14,
	SnapshotApplied = 15,
	Resume = 16,
	SynchronizationFailed = 17,
}

internal static class TurnProtocol
{
	public const int Version = 3;
	public static readonly string ConnectionKey = $"EdgeOfPlain.Turns.{Version}";
	public const long FirstTick = 1;
	public const byte Channel = 0;
	public const DeliveryMethod Delivery = DeliveryMethod.ReliableOrdered;
	public const int FrameHeaderBytes = sizeof(byte) + sizeof(long) + sizeof(int);
	public const int MaxStringCharacters = (ushort.MaxValue - 1) / 3;
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
		}
		return writer;
	}

	public static (int AdministratorId, RoomPlayer[] Players) ReadRoster(NetDataReader reader, BattleRoomOptions options)
	{
		Require(reader.AvailableBytes >= sizeof(int));
		var administrator = reader.GetInt();
		var count = ReadPlayerCount(reader, options);
		Require(administrator >= 0 && administrator < count);
		Require(count <= reader.AvailableBytes / (sizeof(int) + sizeof(ushort)));
		var players = new RoomPlayer[count];
		var names = new HashSet<string>(StringComparer.Ordinal);
		for (var i = 0; i < count; i++)
		{
			Require(reader.AvailableBytes >= sizeof(int));
			var id = reader.GetInt();
			var name = ReadString(reader, options.MaxPlayerNameLength);
			Require(id == i && BattleRoomOptions.IsValidPlayerName(name, options.MaxPlayerNameLength) && names.Add(name));
			players[i] = new RoomPlayer(id, name);
		}
		RequireEnd(reader);
		return (administrator, players);
	}

	public static NetDataWriter Map(Map map, BattleRoomOptions options)
	{
		var bytes = Encoding.UTF8.GetBytes(TomlSerializer.Serialize(map));
		if (bytes.Length > options.MaxMapBytes)
			throw new ArgumentOutOfRangeException(nameof(map), "Map is too large for this room.");
		var writer = Create(TurnMessage.Map);
		WritePayload(writer, bytes);
		return writer;
	}

	public static Map ReadMap(NetDataReader reader, BattleRoomOptions options)
	{
		var bytes = ReadPayload(reader, options.MaxMapBytes);
		Require(bytes.Length > 0);
		RequireEnd(reader);
		return TomlSerializer.Deserialize<Map>(Encoding.UTF8.GetString(bytes));
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
			WritePayload(writer, player.Payload);
		}
		return writer;
	}

	public static TurnFrame ReadFrame(NetDataReader reader, BattleRoomOptions options)
	{
		Require(reader.AvailableBytes >= sizeof(long));
		var tick = reader.GetLong();
		var count = ReadPlayerCount(reader, options);
		Require(count <= reader.AvailableBytes / (2 * sizeof(int)));
		var players = new PlayerCommands[count];
		for (var i = 0; i < count; i++)
		{
			Require(reader.AvailableBytes >= sizeof(int));
			var id = reader.GetInt();
			Require(id == i);
			players[i] = new PlayerCommands(id, ReadPayload(reader, options.MaxPayloadBytes));
		}
		RequireEnd(reader);
		return new TurnFrame(tick, players);
	}

	public static void RequireEnd(NetDataReader reader) => Require(reader.AvailableBytes == 0);

	public static void Require(bool condition)
	{
		if (!condition) throw new InvalidDataException("Invalid turn protocol packet.");
	}

	public static bool IsPacketException(Exception exception)
		=> exception is InvalidDataException or IndexOutOfRangeException or ArgumentException
			or InvalidOperationException or TomlException or FormatException or OverflowException;

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
