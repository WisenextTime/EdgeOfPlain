using EdgeOfPlain.Core.Definitions.Worlds;
using EdgeOfPlain.Core.Game;

namespace EdgeOfPlain.Core.Net.Client
{
	public interface IMapCache
	{
		bool TryGet(WorldKey key, string mapId, string checksum, out Map map);
	}
}
