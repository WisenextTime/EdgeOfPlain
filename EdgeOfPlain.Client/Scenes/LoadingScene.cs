using System;
using System.Threading.Tasks;
using EdgeOfPlain.Client.Resource;
using EdgeOfPlain.Core.Framework.Graphics;
using EdgeOfPlain.Core.Framework.Input;
using EdgeOfPlain.Core.Framework.Scenes;
using EdgeOfPlain.Core.Framework.Time;
using SFML.Graphics;
using SFML.System;
using SfColor = SFML.Graphics.Color;

namespace EdgeOfPlain.Client.Scenes;

public sealed class LoadingScene(
	Action<Action<string>> load,
	Func<IScene> next,
	UiFonts fonts,
	IRenderTarget target,
	TimeSpan? hold = null)
	: Scene
{
	private static readonly ColorF _background = new(16, 17, 21, 255);

	private readonly Action<Action<string>> _load = load ?? throw new ArgumentNullException(nameof(load));
	private readonly Func<IScene> _next = next ?? throw new ArgumentNullException(nameof(next));
	private readonly UiFonts _fonts = fonts ?? throw new ArgumentNullException(nameof(fonts));
	private readonly IRenderTarget _target = target ?? throw new ArgumentNullException(nameof(target));

	private SceneContext? _context;
	private Text? _text;
	private Task? _loading;
	private volatile string _message = "Loading…";
	private double _completedAt;
	private bool _completed;
	private bool _switched;

	public TimeSpan Hold { get; } = hold ?? TimeSpan.FromSeconds(1);

	public override string Name => "Loading";

	public override void Enter(SceneContext context)
	{
		_context = context;

		_text = new Text(_fonts.Bold, _message, 28)
		{
			FillColor = new SfColor(224, 226, 232),
		};

		_loading = Task.Run(() => _load(message => _message = message));
	}

	public override void Update(LogicTime time, IInputState input)
	{
		if (_loading is null || _switched || !_loading.IsCompleted) return;

		if (_loading.IsFaulted)
			throw new InvalidOperationException("Loading the game content failed.", _loading.Exception);

		if (!_completed)
		{
			_completed = true;
			_completedAt = time.TotalSeconds;
			_message = "Success!";
			return;
		}

		if (time.TotalSeconds - _completedAt < Hold.TotalSeconds) return;

		_switched = true;
		_context!.Scenes.Replace(_next());
	}

	public override void Draw(RenderTime time)
	{
		if (_context is null || _text is null) return;

		var window = _context.Window;
		window.ResetCamera();
		window.Clear(_background);

		_text.DisplayedString = _message;

		var bounds = _text.GetLocalBounds();
		var size = window.Size;

		_text.Origin = new Vector2f(bounds.Left + bounds.Width / 2f, bounds.Top + bounds.Height / 2f);
		_text.Position = new Vector2f(size.Width / 2f, size.Height / 2f);

		_target.Draw(_text, RenderStates.Default);
	}

	public override void Exit() => _text?.Dispose();
}
