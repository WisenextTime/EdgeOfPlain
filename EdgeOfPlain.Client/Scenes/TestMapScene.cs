using System;
using System.Numerics;
using EdgeOfPlain.Client.Rendering;
using EdgeOfPlain.Client.UI;
using EdgeOfPlain.Core.Definitions.Worlds;
using EdgeOfPlain.Core.Framework.Events;
using EdgeOfPlain.Core.Framework.Graphics;
using EdgeOfPlain.Core.Framework.Input;
using EdgeOfPlain.Core.Framework.Resource;
using EdgeOfPlain.Core.Framework.Scenes;
using EdgeOfPlain.Core.Framework.Time;
using EdgeOfPlain.Core.World;

namespace EdgeOfPlain.Client.Scenes;

public sealed class TestMapScene : Scene
{
	public const string MapPath = "assets://base/Map/TestMap.toml";

	private const int UiLayer = 1000;

	private MapRenderPipeline? _pipeline;
	private SpriteBatch? _uiSprites;
	private UiRoot? _ui;
	private EventSubscription _resizeSubscription;
	private Vector2 _viewport;

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

		_uiSprites = new SpriteBatch(GameContext.RenderTarget);
		_ui = new UiRoot();

		CreateZoomButton("+", new Vector2(-16f, -16f), 1.25f);
		CreateZoomButton("−", new Vector2(-76f, -16f), 0.8f);

		var size = GameContext.Window.Size;
		ResizeUi(new Vector2(size.Width, size.Height));

		_resizeSubscription = EventBus.Subscribe<WindowResizedEvent>(
			e => ResizeUi(new Vector2(e.Current.Width, e.Current.Height)));
	}

	public override void Update(LogicTime time, IInputState input) => _ui?.Update(input);

	public override void Draw(RenderTime time)
	{
		var camera = GameContext.WorldCamera.Evaluate(time.Alpha);
		GameContext.Window.Camera = camera;

		if (_pipeline is not null)
		{
			_pipeline.Begin(camera);
			_pipeline.End();
		}

		if (_ui is null || _uiSprites is null) return;

		var size = GameContext.Window.Size;
		var viewport = new Vector2(size.Width, size.Height);

		if (viewport != _viewport) ResizeUi(viewport);

		_uiSprites.Begin();
		_ui.Draw(_uiSprites);
		_uiSprites.End();
	}

	public override void Exit()
	{
		_resizeSubscription.Dispose();

		_uiSprites?.Dispose();
		_uiSprites = null;
		_ui = null;

		_pipeline?.Dispose();
		_pipeline = null;
	}

	private void CreateZoomButton(string text, Vector2 offset, float factor)
	{
		var button = new UiButton
		{
			Name = $"Zoom{text}",
			Text = text,
			TextSize = 22,
			Layer = UiLayer,
		};

		button.SetAnchored(new Vector2(1f, 1f), offset, new Vector2(52f, 52f), new Vector2(1f, 1f));
		button.Clicked += _ => Zoom(factor);

		_ui?.Add(button);
	}

	private void Zoom(float factor)
	{
		var camera = GameContext.WorldCamera.Current;

		GameContext.WorldCamera.Set(camera with { Zoom = Math.Clamp(camera.Zoom * factor, 0.25f, 4f) });
	}

	private void ResizeUi(Vector2 viewport)
	{
		if (viewport.X <= 0f || viewport.Y <= 0f) return;

		_viewport = viewport;
		_ui?.Resize(viewport);
	}
}
