using System.Collections.Generic;

namespace EdgeOfPlain.Core.Net
{
	public readonly record struct PlayerCommands(int PlayerId, int Team, byte[] Payload);

	public readonly record struct TurnFrame(long Tick, IReadOnlyList<PlayerCommands> Players);
}
