using EdgeOfPlain.Core.Framework.Graphics;
using EdgeOfPlain.Core.Framework.Input;
using EdgeOfPlain.Core.Framework.Time;

namespace EdgeOfPlain.Core.Framework.Scenes;

public abstract class Scene : IScene
{
	public virtual string Name => GetType().Name;

	public bool IsVisible { get; set; } = true;

	public bool IsEnabled { get; set; } = true;

	public virtual void Enter(SceneContext context) { }

	public virtual void Exit() { }

	public virtual void Update(LogicTime time, IInputState input) { }

	public virtual bool HandleInput(IInputState input) => false;

	public virtual void Draw(RenderTime time) { }
}