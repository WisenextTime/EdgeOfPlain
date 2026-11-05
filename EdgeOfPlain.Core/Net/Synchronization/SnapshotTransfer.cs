using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using LiteNetLib.Utils;

namespace EdgeOfPlain.Core.Net.Synchronization;

internal static class SynchronizationProtocol
{
	public const int ChunkBytes = 48 * 1024;
	public const int ChunksPerPoll = 4;

	public static NetDataWriter Control(TurnMessage message, long id)
	{
		var writer = TurnProtocol.Create(message);
		writer.Put(id);
		return writer;
	}

	public static long ReadId(NetDataReader reader)
	{
		TurnProtocol.Require(reader.AvailableBytes >= sizeof(long));
		return reader.GetLong();
	}

	public static void Observe(Task? task)
	{
		if (task is null) return;
		_ = task.ContinueWith(static failed => _ = failed.Exception, CancellationToken.None,
			TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
	}
}

internal sealed class SnapshotTransmission(long id, byte[] bytes, byte[] hash)
{
	private int _offset;

	public static SnapshotTransmission Create(long id, byte[] bytes)
		=> new(id, bytes, SHA256.HashData(bytes));

	public NetDataWriter Begin()
	{
		var writer = SynchronizationProtocol.Control(TurnMessage.SnapshotBegin, id);
		writer.Put(bytes.Length);
		writer.Put(hash);
		return writer;
	}

	public bool SendChunks(Action<NetDataWriter> send)
	{
		for (var i = 0; i < SynchronizationProtocol.ChunksPerPoll && _offset < bytes.Length; i++)
		{
			var count = Math.Min(SynchronizationProtocol.ChunkBytes, bytes.Length - _offset);
			var writer = SynchronizationProtocol.Control(TurnMessage.SnapshotChunk, id);
			writer.Put(_offset);
			TurnProtocol.WritePayload(writer, bytes.AsSpan(_offset, count));
			send(writer);
			_offset += count;
		}
		return _offset == bytes.Length;
	}
}

internal sealed class SnapshotReception
{
	private readonly byte[] _bytes;
	private readonly byte[] _hash;
	private int _received;

	public SnapshotReception(NetDataReader reader)
	{
		TurnProtocol.Require(reader.AvailableBytes == sizeof(int) + SHA256.HashSizeInBytes);
		var length = reader.GetInt();
		TurnProtocol.Require(length > 0);
		_hash = new byte[SHA256.HashSizeInBytes];
		reader.GetBytes(_hash, 0, _hash.Length);
		_bytes = new byte[length];
	}

	public bool Append(NetDataReader reader)
	{
		TurnProtocol.Require(reader.AvailableBytes >= sizeof(int));
		var offset = reader.GetInt();
		var chunk = TurnProtocol.ReadPayload(reader, SynchronizationProtocol.ChunkBytes);
		TurnProtocol.RequireEnd(reader);
		TurnProtocol.Require(offset == _received && chunk.Length > 0 && chunk.Length <= _bytes.Length - _received);
		chunk.CopyTo(_bytes, _received);
		_received += chunk.Length;
		return _received == _bytes.Length;
	}

	public Task<byte[]> CompleteAsync(CancellationToken cancellationToken)
	{
		TurnProtocol.Require(_received == _bytes.Length);
		return Task.Run(() =>
		{
			cancellationToken.ThrowIfCancellationRequested();
			TurnProtocol.Require(CryptographicOperations.FixedTimeEquals(SHA256.HashData(_bytes), _hash));
			return _bytes;
		}, cancellationToken);
	}
}
