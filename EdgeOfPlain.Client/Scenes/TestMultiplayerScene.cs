using System;
using System.Globalization;
using System.Numerics;
using EdgeOfPlain.Client.Net;
using EdgeOfPlain.Client.Rendering;
using EdgeOfPlain.Client.UI;
using EdgeOfPlain.Core.Definitions.Worlds;
using EdgeOfPlain.Core.Framework.Graphics;
using EdgeOfPlain.Core.Framework.Input;
using EdgeOfPlain.Core.Framework.Resource;
using EdgeOfPlain.Core.Framework.Scenes;
using EdgeOfPlain.Core.Framework.Time;
using EdgeOfPlain.Core.Net;

namespace EdgeOfPlain.Client.Scenes;

public sealed class TestMultiplayerScene : Scene
{
	private const int UiLayer = 1000;
	private const int NameFocus = 0;
	private const int AddressFocus = 1;
	private static readonly ColorF _background = new(16, 17, 21, 255);

	private SceneContext? _context;
	private SpriteBatch? _sprites;
	private UiRoot? _ui;
	private MatchSession? _session;
	private string _name = "Player";
	private string _address = "127.0.0.1:27015";
	private int _focus = NameFocus;
	private string _status = "输入名字，创建或加入房间。";
	private Vector2 _viewport;

	public TestMultiplayerScene() { }

	public TestMultiplayerScene(string notice)
	{
		_status = notice;
	}

	public override string Name => "Multiplayer";

	public override void Enter(SceneContext context)
	{
		_context = context;
		_sprites = new SpriteBatch(GameContext.RenderTarget);
		_ui = new UiRoot();

		AddButton("创建房间", 40f, 160f, CreateRoom);
		AddButton("加入房间", 40f, 216f, JoinRoom);
		AddButton("开始对局", 220f, 160f, StartMatch);
		AddButton("添加 AI", 220f, 216f, AddAi);
		AddButton("离开房间", 220f, 272f, LeaveRoom);

		var size = GameContext.Window.Size;
		ResizeUi(new Vector2(size.Width, size.Height));
	}

	public override bool HandleInput(IInputState input)
	{
		_ui?.Update(input);

		if (_session is null)
		{
			if (input.WasKeyPressed(Key.Tab)) _focus = (_focus + 1) % 2;
			if (_focus == NameFocus)
				_name = EditText(_name, input, 24);
			else
				_address = EditText(_address, input, 64);
		}

		return true;
	}

	public override void Update(LogicTime time, IInputState input)
	{
		if (_session is null) return;

		_session.Poll();
		UpdateStatus();

		if (_session.IsStarted)
		{
			var session = _session;
			_session = null;
			_context!.Scenes.Replace(new TestMapScene(session));
		}
	}

	public override void Draw(RenderTime time)
	{
		var window = GameContext.Window;
		window.ResetCamera();
		window.Clear(_background);

		if (_sprites is null) return;

		var size = window.Size;
		var viewport = new Vector2(size.Width, size.Height);
		if (viewport != _viewport) ResizeUi(viewport);

		_sprites.Begin();
		_sprites.DrawText("Edge of Plain — 联机大厅", 30, 40f, 36f, UiLayer);
		_sprites.DrawText("名字", 20, 40f, 96f, UiLayer);
		DrawField(_sprites, _name, 140f, 88f, _focus == NameFocus);
		_sprites.DrawText("房间地址（IP:端口）", 20, 40f, 128f, UiLayer);
		DrawField(_sprites, _address, 250f, 120f, _focus == AddressFocus);
		_sprites.DrawText("Tab 切换输入框", 16, 40f, 320f, UiLayer);
		_sprites.DrawText(_status, 18, 40f, 350f, UiLayer);
		DrawRoster(_sprites);
		_ui?.Draw(_sprites);
		_sprites.End();
	}

	public override void Exit()
	{
		_sprites?.Dispose();
		_sprites = null;
		_ui = null;
		_session?.Dispose();
		_session = null;
	}

	private static void DrawField(ISpriteBatch batch, string value, float x, float y, bool focused)
	{
		var prefix = focused ? "▸ " : "  ";
		batch.DrawText(prefix + value, 20, x, y, UiLayer + 1);
		if (!focused) return;
		var measured = batch.MeasureText(prefix + value, 20);
		batch.DrawText("_", 20, x + measured.X, y, UiLayer + 1);
	}

	private void DrawRoster(ISpriteBatch batch)
	{
		if (_session is null)
		{
			batch.DrawText("尚未连接房间。", 18, 40f, 386f, UiLayer);
			return;
		}

		batch.DrawText($"房间状态：{_session.Client.Room.State}", 18, 40f, 380f, UiLayer);
		var y = 408f;
		foreach (var player in _session.Client.Room.Players)
		{
			var admin = player.Id == _session.Client.Room.AdministratorId ? "（管理员）" : string.Empty;
			var kind = player.Kind == RoomPlayerKind.Ai ? "AI" : "玩家";
			batch.DrawText($"{player.Id}  {player.Name}  {kind}  队伍 {player.Team}{admin}", 18, 40f, y,
				UiLayer);
			y += 24f;
		}
	}

	private void AddButton(string text, float x, float y, Action clicked)
	{
		var button = new UiButton
		{
			Name = text,
			Text = text,
			TextSize = 18,
			Layer = UiLayer,
		};
		button.SetAnchored(new Vector2(0f, 0f), new Vector2(x, y), new Vector2(160f, 42f));
		button.Clicked += _ => clicked();
		_ui?.Add(button);
	}

	private void CreateRoom()
	{
		if (_session is not null) return;
		if (!TryParseAddress(_address, out _, out var port)) return;
		try
		{
			_session = MatchSession.Host(_name, port);
			_status = $"房间已创建，监听端口 {port}，等待其他玩家加入。";
		}
		catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
		{
			_status = $"创建房间失败：{exception.Message}";
		}
	}

	private void JoinRoom()
	{
		if (_session is not null) return;
		if (!TryParseAddress(_address, out var host, out var port)) return;
		try
		{
			_session = MatchSession.Join(_name, host, port);
			_status = $"正在连接 {host}:{port}…";
		}
		catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
		{
			_status = $"加入房间失败：{exception.Message}";
		}
	}

	private void StartMatch()
	{
		if (_session is not { IsAdministrator: true, Server: not null }) return;
		try
		{
			_session.StartMatch(AssetsManager.LoadResource<Map>(TestMapScene.MapPath));
			_status = "正在加载地图，等待所有玩家就绪…";
		}
		catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
		{
			_status = $"开始对局失败：{exception.Message}";
		}
	}

	private void AddAi()
	{
		if (_session is not { IsAdministrator: true }) return;
		try
		{
			var index = 1;
			foreach (var player in _session.Client.Room.Players)
				if (player.Kind == RoomPlayerKind.Ai) index++;
			_session.AddAi($"Bot{index}");
			_status = $"已添加 AI：Bot{index}。";
		}
		catch (InvalidOperationException exception)
		{
			_status = exception.Message;
		}
	}

	private void LeaveRoom()
	{
		_session?.Dispose();
		_session = null;
		_status = "已离开房间。";
	}

	private void UpdateStatus()
	{
		if (_session is null) return;
		if (_session.Client.RejectionReason is { } reason)
			_status = $"连接断开：{reason}";
		else if (_session.Client.IsStarted)
			_status = "对局开始，正在进入地图…";
		else if (_session.Core is not null)
			_status = $"世界已就绪，等待管理员开始（{_session.Client.Room.State}）。";
	}

	private static bool TryParseAddress(string text, out string host, out int port)
	{
		host = string.Empty;
		port = 27015;
		if (string.IsNullOrWhiteSpace(text)) return false;
		var separator = text.LastIndexOf(':');
		if (separator <= 0 || separator == text.Length - 1) return false;
		host = text[..separator].Trim();
		if (!int.TryParse(text[(separator + 1)..].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out port))
			return false;
		return port is > 0 and <= 65535 && host.Length > 0;
	}

	private static string EditText(string text, IInputState input, int maxLength)
	{
		if (text.Length > maxLength) text = text[..maxLength];
		if (input.WasKeyPressed(Key.Backspace) && text.Length > 0) text = text[..^1];
		if (text.Length >= maxLength) return text;
		var character = ReadCharacter(input);
		return character == '\0' ? text : text + character;
	}

	private static char ReadCharacter(IInputState input)
	{
		for (var key = (int)Key.A; key <= (int)Key.Z; key++)
			if (input.WasKeyPressed((Key)key)) return (char)('a' + (key - (int)Key.A));
		for (var key = (int)Key.Num0; key <= (int)Key.Num9; key++)
			if (input.WasKeyPressed((Key)key)) return (char)('0' + (key - (int)Key.Num0));
		if (input.WasKeyPressed(Key.Period)) return '.';
		if (input.WasKeyPressed(Key.Hyphen)) return '-';
		if (input.WasKeyPressed(Key.Space)) return ' ';
		if (input.WasKeyPressed(Key.Equal)) return '_';
		return '\0';
	}

	private void ResizeUi(Vector2 viewport)
	{
		if (viewport.X <= 0f || viewport.Y <= 0f) return;
		_viewport = viewport;
		_ui?.Resize(viewport);
	}
}
