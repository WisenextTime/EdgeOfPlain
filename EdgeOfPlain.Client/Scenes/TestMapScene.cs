using System;
using System.Numerics;
using Arch.Core;
using EdgeOfPlain.Client.Rendering;
using EdgeOfPlain.Client.UI;
using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Content.Systems;
using EdgeOfPlain.Core.Content.Systems.Commands;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Definitions.Worlds;
using EdgeOfPlain.Core.Framework.Events;
using EdgeOfPlain.Core.Framework.Graphics;
using EdgeOfPlain.Core.Framework.Input;
using EdgeOfPlain.Core.Framework.Mods;
using EdgeOfPlain.Core.Framework.Resource;
using EdgeOfPlain.Core.Framework.Scenes;
using EdgeOfPlain.Core.Framework.Time;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Game.Systems;

namespace EdgeOfPlain.Client.Scenes;

public sealed class TestMapScene : Scene
{
	public const string MapPath = "assets://base/Map/testMap.toml";

	private const int UiLayer = 1000;
	private const int UnitLayer = 10;

	private const float UnitVisualSize = 40f;
	private const float DeleteRadius = 24f;

	private static readonly Vector2 _unitOrigin = new(UnitVisualSize / 2f, UnitVisualSize / 2f);

	private MapRenderPipeline? _pipeline;
	private SpriteBatch? _uiSprites;
	private SpriteBatch? _unitSprites;
	private UiRoot? _ui;
	private GameWorld? _gameWorld;
	private UnitDef? _unitDef;
	private string _unitTexture = "";
	private EventSubscription _resizeSubscription;
	private Vector2 _viewport;

	public Map Map { get; private set; }

	public GameMap? GameMap { get; private set; }

	public override void Enter(SceneContext context)
	{
		Map = AssetsManager.LoadResource<Map>(MapPath);
		GameMap = GameMap.Build(Map);
		_pipeline = new MapRenderPipeline(GameMap);

		_gameWorld = new GameWorld(GameMap);
		_unitDef = FindUnitDefinition();
		_unitTexture = _unitDef?.ResolveResourcePath(_unitDef.Texture) ?? "";

		var center = new Vector2(
			GameMap.Bounds.Width / 2f,
			GameMap.Bounds.Height / 2f);

		GameContext.WorldCamera.Reset(new Camera2D(center));

		_uiSprites = new SpriteBatch(GameContext.RenderTarget);
		_unitSprites = new SpriteBatch(GameContext.RenderTarget);
		_ui = new UiRoot();

		CreateZoomButton("+", new Vector2(-16f, -16f), 1.25f);
		CreateZoomButton("−", new Vector2(-76f, -16f), 0.8f);

		var size = GameContext.Window.Size;
		ResizeUi(new Vector2(size.Width, size.Height));

		_resizeSubscription = EventBus.Subscribe<WindowResizedEvent>(
			e => ResizeUi(new Vector2(e.Current.Width, e.Current.Height)));
	}

	public override void Update(LogicTime time, IInputState input)
	{
		_ui?.Update(input);

		if (_gameWorld is null) return;

		var overUi = _ui?.PointerOverUi ?? false;

		if (!overUi && input.WasMouseButtonPressed(MouseButton.Left)) QueueSpawn(input);
		if (input.WasMouseButtonPressed(MouseButton.Right)) QueueDelete(input);

		_gameWorld.Step();
	}

	public override void Draw(RenderTime time)
	{
		var camera = GameContext.WorldCamera.Evaluate(time.Alpha);
		GameContext.Window.Camera = camera;

		if (_pipeline is not null)
		{
			_pipeline.Begin(camera);
			_pipeline.End();
		}

		DrawUnits(camera);

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

		_unitSprites?.Dispose();
		_unitSprites = null;

		_gameWorld?.Dispose();
		_gameWorld = null;

		_pipeline?.Dispose();
		_pipeline = null;
	}

	private static UnitDef? FindUnitDefinition()
	{
		foreach (var def in ContentStore.All<UnitDef>()) return def;
		return null;
	}

	private void QueueSpawn(IInputState input)
	{
		if (_gameWorld is null || _unitDef is null) return;

		var transform = new TransformComponent
		{
			Position = ToWorld(input.MousePosition),
			Rotation = Random.Shared.NextSingle() * MathF.Tau,
		};

		_gameWorld.Commands.Enqueue(new SpawnUnitCommand(_unitDef, transform));
	}

	private void QueueDelete(IInputState input)
	{
		if (_gameWorld is null) return;

		var point = ToWorld(input.MousePosition);
		var queue = _gameWorld.QueueView();
		var description = new QueryDescription().WithAll<TransformComponent>();

		EntityHandle target = default;
		var found = false;
		var best = DeleteRadius * DeleteRadius;

		queue.Query(description, (in EntityHandle handle, in TransformComponent transform) =>
		{
			var distance = Vector2.DistanceSquared(transform.Position, point);
			if (distance > best) return;

			best = distance;
			target = handle;
			found = true;
		});

		if (found) _gameWorld.Commands.Enqueue(new DeleteUnitCommand(target));
	}

	private void DrawUnits(Camera2D camera)
	{
		if (_unitSprites is null || _gameWorld is null || _unitTexture.Length == 0) return;

		var description = new QueryDescription().WithAll<TransformComponent>();
		var queue = _gameWorld.QueueView();
		var sprites = _unitSprites;

		sprites.Begin(camera);
		queue.Query(description, (in EntityHandle handle, in TransformComponent transform) =>
		{
			sprites.Draw(
				new RenderMetadata
				{
					Texture = _unitTexture,
					Layer = UnitLayer,
					Size = new Vector2(UnitVisualSize),
					Origin = _unitOrigin,
					Rotation = transform.Rotation,
				},
				transform.Position.X,
				transform.Position.Y);
		});
		sprites.End();
	}

	private Vector2 ToWorld(Vector2 screen)
	{
		var size = GameContext.Window.Size;
		return GameContext.WorldCamera.Current.ScreenToWorld(screen, new Vector2(size.Width, size.Height));
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
