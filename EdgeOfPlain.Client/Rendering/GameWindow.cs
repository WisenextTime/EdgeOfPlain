using System;
using System.Drawing;
using System.Numerics;
using EdgeOfPlain.Core.Framework.Graphics;
using SFML.Graphics;
using SFML.System;
using SFML.Window;
using SfColor = SFML.Graphics.Color;

namespace EdgeOfPlain.Client.Rendering;

public sealed class GameWindow : IGameWindow, IDisposable
{
	private readonly RenderWindow _window;
	private Rectangle _viewport;
	private bool _viewportDirty;
	private bool _viewportIsFull = true;
	private Camera2D? _camera;

	public GameWindow(uint width, uint height, string title)
	{
		_window = new RenderWindow(new VideoMode(new Vector2u(width, height)), title, Styles.Default, State.Windowed);
		_window.SetFramerateLimit(60);
		_window.Closed += (_, _) => _window.Close();

		_window.Resized += (_, _) =>
		{
			if (_viewportIsFull)
			{
				var size = _window.Size;
				_viewport = new Rectangle(0, 0, (int)size.X, (int)size.Y);
			}

			_viewportDirty = true;
		};

		_window.KeyPressed += (_, e) => KeyPressed?.Invoke(e.Code);
		_window.KeyReleased += (_, e) => KeyReleased?.Invoke(e.Code);
		_window.MouseButtonPressed += (_, e) => MouseButtonPressed?.Invoke(e.Button);
		_window.MouseButtonReleased += (_, e) => MouseButtonReleased?.Invoke(e.Button);
		_window.MouseMoved += (_, e) => MouseMoved?.Invoke(e.Position.X, e.Position.Y);
		_window.MouseWheelScrolled += (_, e) => MouseWheelScrolled?.Invoke(e.Delta);
		_window.LostFocus += (_, _) => FocusLost?.Invoke();

		_viewport = new Rectangle(0, 0, (int)width, (int)height);
		ApplyView();
	}

	public event Action<Keyboard.Key>? KeyPressed;

	public event Action<Keyboard.Key>? KeyReleased;

	public event Action<Mouse.Button>? MouseButtonPressed;

	public event Action<Mouse.Button>? MouseButtonReleased;

	public event Action<float, float>? MouseMoved;

	public event Action<float>? MouseWheelScrolled;

	public event Action? FocusLost;

	public IRenderTarget Target => _window;

	public bool IsOpen => _window.IsOpen;

	public Size Size
	{
		get
		{
			var size = _window.Size;
			return new Size((int)size.X, (int)size.Y);
		}
	}

	public Rectangle Viewport
	{
		get => _viewport;
		set
		{
			_viewport = value;
			var size = Size;
			_viewportIsFull = value.X == 0 && value.Y == 0 && value.Width == size.Width && value.Height == size.Height;
			ApplyView();
		}
	}
	public Camera2D Camera
	{
		get => _camera ?? PixelSpaceCamera();
		set
		{
			_camera = value;
			ApplyView();
		}
	}

	public void ResetCamera()
	{
		_camera = null;
		ApplyView();
	}

	public void PollEvents() => _window.DispatchEvents();

	public void WaitForEvents(TimeSpan timeout)
		=> _window.WaitAndDispatchEvents(
			timeout <= TimeSpan.Zero ? Time.Zero : Time.FromSeconds((float)timeout.TotalSeconds));

	public void ActivateContext()
	{
		if (_window.SetActive(true)) return;

		throw new InvalidOperationException(
			"Failed to activate the OpenGL context on this thread. " +
			"The thread that created the window is most likely still holding it: call DeactivateContext() there first.");
	}

	public void DeactivateContext() => _window.SetActive(false);

	public void Clear(ColorF color)
	{
		if (_viewportDirty)
		{
			_viewportDirty = false;
			ApplyView();
		}

		_window.Clear(new SfColor(color.R, color.G, color.B, color.A));
	}

	public void Present() => _window.Display();

	public void Dispose() => _window.Dispose();

	private Camera2D PixelSpaceCamera()
	{
		var size = Size;
		return new Camera2D(new Vector2(size.Width / 2f, size.Height / 2f));
	}

	private void ApplyView()
	{
		var size = Size;
		if (size.Width <= 0 || size.Height <= 0) return;

		var camera = Camera;
		var worldSize = camera.WorldSize(new Vector2(size.Width, size.Height));

		_window.SetView(new View(
			new Vector2f(camera.Position.X, camera.Position.Y),
			new Vector2f(worldSize.X, worldSize.Y))
		{
			Rotation = Angle.FromRadians(camera.Rotation),
			Viewport = new FloatRect(
				new Vector2f((float)_viewport.X / size.Width, (float)_viewport.Y / size.Height),
				new Vector2f((float)_viewport.Width / size.Width, (float)_viewport.Height / size.Height)),
		});
	}
}
