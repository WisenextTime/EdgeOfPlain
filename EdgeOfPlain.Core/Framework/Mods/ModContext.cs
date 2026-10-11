using EdgeOfPlain.Core.Definitions;

namespace EdgeOfPlain.Core.Framework.Mods;

public class ModContext
{
	public void AddContent(ContentDef content) => ContentStore.Add(content);
}