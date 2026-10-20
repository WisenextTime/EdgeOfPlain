using System.Drawing;
using EdgeOfPlain.Core.Framework.Scenes;
using EdgeOfPlain.Core.World;

namespace EdgeOfPlain.Core.Framework.Events;

public enum SceneChangeKind
{
	Push,
	Pop,
	Replace,
	PopTo,
	Clear,
}

public readonly record struct SceneChangedEvent(SceneChangeKind Kind, IScene? Previous, IScene? Current);

public readonly record struct WindowResizedEvent(Size Previous, Size Current);

public readonly record struct MapLoadedEvent(Map Source);