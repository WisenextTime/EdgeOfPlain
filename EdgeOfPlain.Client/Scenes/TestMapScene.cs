using System.Numerics;
using EdgeOfPlain.Client.Rendering;
using EdgeOfPlain.Core.Framework.Graphics;
using EdgeOfPlain.Core.Framework.Resource;
using EdgeOfPlain.Core.Framework.Scenes;
using EdgeOfPlain.Core.Framework.Time;
using EdgeOfPlain.Core.World;

namespace EdgeOfPlain.Client.Scenes;

public sealed class TestMapScene : Scene
{
	public const string MapPath = "assets://base/Map/TestMap.toml";

	private MapRenderPipeline? _pipeline;

	public Map Map { get; private set; }

	public GameMap? GameMap { get; private set; }

	public override void Enter(SceneContext context)
	{
		Map = AssetsManager.LoadResource<Map>(MapPath);
		GameMap = GameMap.Build(Map);
		_pipeline = new MapRenderPipeline(GameMap);

		var center = new Vector2(
			GameMap.Bounds.Width / 2f,
			GameMap.Bounds.Height / 2f);

		GameContext.WorldCamera.Reset(new Camera2D(center));
	}

	public override void Draw(RenderTime time)
	{
		var camera = GameContext.WorldCamera.Evaluate(time.Alpha);
		GameContext.Window.Camera = camera;

		if (_pipeline is null) return;

		_pipeline.Begin(camera);
		_pipeline.End();
	}

	public override void Exit()
	{
		_pipeline?.Dispose();
		_pipeline = null;
	}
}
