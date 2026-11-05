using System;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Arch.Core;
using EdgeOfPlain.Client.Rendering;
using EdgeOfPlain.Client.UI;
using EdgeOfPlain.Core;
using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Content.Systems;
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
using EdgeOfPlain.Core.Game.Waypoint;
using EdgeOfPlain.Core.MathLib;
using EdgeOfPlain.Core.Net;
using EdgeOfPlain.Core.Net.Client;
using EdgeOfPlain.Core.Net.Server;
using FixedMathSharp;

namespace EdgeOfPlain.Client.Scenes;

public sealed class TestMapScene : Scene
{
	public const string MapPath = "assets://base/Map/testMap.toml";

	private const int UiLayer = 1000;
	private const int UnitLayer = 10;

	private const float UnitVisualSize = 40f;
	private const float DeleteRadius = 24f;
	private const int MaxUnitsPerOrder = 512;

	private static readonly Vector2 _unitOrigin = new(UnitVisualSize / 2f, UnitVisualSize / 2f);

	private MapRenderPipeline? _pipeline;
	private SpriteBatch? _uiSprites;
	private SpriteBatch? _unitSprites;
	private UiRoot? _ui;
	private GameCore? _core;
	private GameWorld? _gameWorld;
	private ServerNetNode? _server;
	private ClientNetNode? _client;
	private readonly NetworkCommandInput _pendingInput = new();
	private long _simulationTick;
	private bool _waitingFrame;
	private Fixed64 _stepTimer;
	private static readonly Fixed64 _stepDuration = GameLoop.DefaultFixedStep;
	private UnitDef? _unitDef;
	private string _unitTexture = "";
	private EventSubscription _resizeSubscription;
	private Vector2 _viewport;

	public Map Map { get; private set; }

	public GameMap? GameMap { get; private set; }

	public Task SynchronizeAsync(CancellationToken cancellationToken = default)
		=> (_server ?? throw new InvalidOperationException("The scene has no room.")).SynchronizeAsync(cancellationToken);

	public override void Enter(SceneContext context)
	{
		Map = AssetsManager.LoadResource<Map>(MapPath);
		_unitDef = FindUnitDefinition();
		_unitTexture = _unitDef?.ResolveResourcePath(_unitDef.Texture) ?? "";
		if (Globals.ModChecksum is null || !CommandRegistry.IsSealed)
			throw new InvalidOperationException("Mods and commands must be loaded before starting the local room.");
		_server = new ServerNetNode(0, new BattleRoomOptions { MaxPlayers = 1 });
		_client = new ClientNetNode(IPAddress.Loopback.ToString(), _server.LocalPort);
		_client.Synchronized += OnSynchronized;

		_uiSprites = new SpriteBatch(GameContext.RenderTarget);
		_unitSprites = new SpriteBatch(GameContext.RenderTarget);
		_ui = new UiRoot();

		CreateZoomButton("+", new Vector2(-16f, -16f), 1.25f);
		CreateZoomButton("−", new Vector2(-76f, -16f), 0.8f);

		var size = GameContext.Window.Size;
		ResizeUi(new Vector2(size.Width, size.Height));

		_resizeSubscription =
			EventBus.Subscribe<WindowResizedEvent>(e => ResizeUi(new Vector2(e.Current.Width, e.Current.Height)));
	}

	public override bool HandleInput(IInputState input)
	{
		_ui?.Update(input);

		if (_gameWorld is null || _client is not { IsStarted: true }) return false;

		var overUi = _ui?.PointerOverUi ?? false;

		if (!overUi && input.WasMouseButtonPressed(MouseButton.Left)) QueueSpawn(input);
		if (input.WasMouseButtonPressed(MouseButton.Right)) QueueDelete(input);

		return true;
	}

	public override void Update(LogicTime time, IInputState input)
	{
		if (_server is null || _client is null) return;
		PollNetwork();

		if (_server.Room.State == BattleRoomState.Lobby &&
		    _server.PlayerCount == _server.Room.Options.MaxPlayers && _client.PlayerId is not null)
			_server.StartMatch(Map);

		if (_client.PendingMap is { } receivedMap && _gameWorld is null)
		{
			GameMap = GameMap.Build(receivedMap);
			_pipeline = new MapRenderPipeline(GameMap);
			_core = new GameCore();
			_gameWorld = _core.CreateWorld(WorldKey.Primary, GameMap);
			_client.BindGameCore(_core, time.Delta);
			var center = new Vector2(GameMap.Bounds.Width / 2f, GameMap.Bounds.Height / 2f);
			GameContext.WorldCamera.Reset(new Camera2D(center));
			_client.MarkWorldReady(_gameWorld);
		}

		if (!_client.IsStarted || _gameWorld is null || _core is null) return;

		if (_waitingFrame && _client.TryStepWorld(_simulationTick + 1, _core, time.Delta))
		{
			_simulationTick++;
			_waitingFrame = false;
			_stepTimer = Fixed64.Zero;
		}

		if (!_waitingFrame)
		{
			_client.Submit(_simulationTick + 1, _pendingInput);
			_waitingFrame = true;

			PollNetwork();

			if (_client.TryStepWorld(_simulationTick + 1, _core, time.Delta))
			{
				_simulationTick++;
				_waitingFrame = false;
				_stepTimer = Fixed64.Zero;
			}
		}
	}

	public override void Draw(RenderTime time)
	{
		PollNetwork();

		_stepTimer += time.FrameDelta;
		var alpha = FixedMath.Clamp(_stepTimer / _stepDuration, Fixed64.Zero, Fixed64.One);

		var camera = GameContext.WorldCamera.Evaluate((float)time.Alpha);
		GameContext.Window.Camera = camera;

		if (_pipeline is not null)
		{
			_pipeline.Begin(camera);
			_pipeline.End();
		}

		if (_gameWorld is not null) DrawUnits(camera, alpha);

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

		_gameWorld = null;
		_core?.Dispose();
		_core = null;
		_client?.Dispose();
		_client = null;
		_server?.Dispose();
		_server = null;

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

		var position = ToWorld(input.MousePosition);
		var rotation = (Fixed64)(Random.Shared.NextSingle() * MathF.Tau);
		var transform = new TransformComponent
		{
			Position = position,
			PreviousPosition = position,
			Rotation = rotation,
			PreviousRotation = rotation,
		};

		_pendingInput.Add(new SpawnEntityCommand(_unitDef, transform, 0) { World = _gameWorld.Key });
	}

	private void QueueDelete(IInputState input)
	{
		if (_gameWorld is null) return;

		var point = ToWorld(input.MousePosition);
		var description = new QueryDescription().WithAll<TransformComponent>();

		EntityHandle target = default;
		var found = false;
		var best = (Fixed64)(DeleteRadius * DeleteRadius);

		_gameWorld.World.Query(description, (ref EntityHandle handle, ref TransformComponent transform) =>
		{
			var distance = (transform.Position - point).MagnitudeSquared;
			if (distance > best) return;

			best = distance;
			target = handle;
			found = true;
		});

		if (found)
		{
			if (_gameWorld.TryGetId(target, out var id))
				_pendingInput.Add(new DeleteEntityCommand(id) { World = _gameWorld.Key });
			return;
		}

		var handles = new EntityHandle[MaxUnitsPerOrder];
		var count = _gameWorld.CollectEntities(description, handles);

		var selected = Math.Min(count, handles.Length);
		if (selected == 0) return;
		_pendingInput.Add(new WaypointCommand(_gameWorld.Select(handles.AsSpan(0, selected).ToArray()),
			WaypointType.Move, WaypointAddType.Replace, new GroundTarget(point))
		{
			World = _gameWorld.Key,
		});
	}

	private void PollNetwork()
	{
		_server?.PollEvents();
		_client?.PollEvents();
	}

	private void OnSynchronized()
	{
		_gameWorld = _core!.PrimaryWorld;
		GameMap = _gameWorld!.Map;
		Map = GameMap.Export();
		_pipeline?.Dispose();
		_pipeline = new MapRenderPipeline(GameMap);
		_simulationTick = _client!.AppliedTick;
		_waitingFrame = _client.HasPendingSubmission;
		_stepTimer = Fixed64.Zero;
	}

	private void DrawUnits(Camera2D camera, Fixed64 alpha)
	{
		if (_unitSprites is null || _gameWorld is null || _unitTexture.Length == 0) return;

		var description = new QueryDescription().WithAll<TransformComponent>();
		var sprites = _unitSprites;

		sprites.Begin(camera);
		_gameWorld.World.Query(description, (ref EntityHandle handle, ref TransformComponent transform) =>
		{
			var position = Vector2d.Lerp(transform.PreviousPosition, transform.Position, alpha);
			var rotation = AngleExtension.LerpAngle(transform.PreviousRotation, transform.Rotation, alpha);
			sprites.Draw(
				new RenderMetadata
				{
					Texture = _unitTexture,
					Layer = UnitLayer,
					Size = new Vector2(UnitVisualSize),
					Origin = _unitOrigin,
					Rotation = (float)rotation,
				},
				(float)position.X,
				(float)position.Y);
		});
		sprites.End();
	}

	private Vector2d ToWorld(Vector2 screen)
	{
		var size = GameContext.Window.Size;
		var world = GameContext.WorldCamera.Current.ScreenToWorld(screen, new Vector2(size.Width, size.Height));
		return new Vector2d((Fixed64)world.X, (Fixed64)world.Y);
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
