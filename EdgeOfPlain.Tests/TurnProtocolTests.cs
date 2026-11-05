using System;
using System.IO;
using EdgeOfPlain.Core.Net;
using LiteNetLib.Utils;
using Xunit;

namespace EdgeOfPlain.Tests;

public sealed class TurnProtocolTests
{
	[Theory]
	[InlineData(-1, 0)]
	[InlineData(9, 9)]
	[InlineData(8, 7)]
	[InlineData(7, 8)]
	public void SubmissionRejectsNegativeOversizedTruncatedAndTrailingPayloads(int declaredLength, int actualLength)
	{
		var writer = new NetDataWriter();
		writer.Put(1L);
		writer.Put(declaredLength);
		writer.Put(new byte[actualLength]);
		var reader = new NetDataReader(writer.Data, 0, writer.Length);
		Assert.Throws<InvalidDataException>(() => TurnProtocol.ReadSubmission(reader,
			new BattleRoomOptions { MaxPayloadBytes = 8 }));
	}

	[Fact]
	public void FrameRejectsImpossiblePlayerCountBeforeAllocating()
	{
		var writer = new NetDataWriter();
		writer.Put(1L);
		writer.Put(int.MaxValue);
		var reader = new NetDataReader(writer.Data, 0, writer.Length);
		Assert.Throws<InvalidDataException>(() => TurnProtocol.ReadFrame(reader, BattleRoomOptions.Default));
	}

	[Fact]
	public void RosterRejectsANameThatExtendsPastThePacket()
	{
		var writer = new NetDataWriter();
		writer.Put(1);
		writer.Put(0);
		writer.Put((ushort)10);
		writer.Put((byte)'A');
		var reader = new NetDataReader(writer.Data, 0, writer.Length);
		var exception = Record.Exception(() => TurnProtocol.ReadRoster(reader, BattleRoomOptions.Default));
		Assert.NotNull(exception);
		Assert.True(TurnProtocol.IsPacketException(exception));
	}

	[Theory]
	[InlineData(0, 4096, 1024, 32)]
	[InlineData(8, 1, 1024, 32)]
	[InlineData(8, 4096, 0, 32)]
	[InlineData(8, 4096, 1024, 0)]
	[InlineData(8, 4096, 1024, ushort.MaxValue)]
	[InlineData(int.MaxValue, int.MaxValue, 1024, 32)]
	public void InvalidRulesAreRejectedBeforeOpeningConnections(int players, int payload, int map, int name)
	{
		var options = new BattleRoomOptions
		{
			MaxPlayers = players, MaxPayloadBytes = payload, MaxMapBytes = map, MaxPlayerNameLength = name,
		};
		Assert.Throws<ArgumentOutOfRangeException>(() => new BattleRoom("checksum", options));
	}
}
