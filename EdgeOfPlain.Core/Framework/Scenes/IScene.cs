using EdgeOfPlain.Core.Framework.Graphics;
using EdgeOfPlain.Core.Framework.Input;
using EdgeOfPlain.Core.Framework.Time;

namespace EdgeOfPlain.Core.Framework.Scenes;

public interface IScene
{
	string Name { get; }

	bool IsVisible { get; }

	bool IsEnabled { get; }

	void Enter(SceneContext context);

	void Exit();

	void Update(LogicTime time, IInputState input);

	bool HandleInput(IInputState input);

	void Draw(IRenderPipeline pipeline, RenderTime time);
}
