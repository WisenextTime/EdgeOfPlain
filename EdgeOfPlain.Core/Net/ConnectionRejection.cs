namespace EdgeOfPlain.Core.Net;

public enum ConnectionRejection : byte
{
	None = 0,
	ModChecksumMismatch = 1,
	InvalidPlayerName = 2,
	RoomFull = 3,
	RoomNotJoinable = 4,
	InvalidPacket = 5,
	JoinTimeout = 6,
	LoadingTimeout = 7,
	TurnTimeout = 8,
	ConnectionFailed = 9,
	ConnectionLost = 10,
	ProtocolMismatch = 11,
	SynchronizationTimeout = 12,
	SynchronizationFailed = 13,
	RoomClosed = 14,
}

public enum ClientConnectionStatus { Connecting, Joining, Connected, Disconnected, Disposed }
