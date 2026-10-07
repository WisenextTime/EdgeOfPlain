using System;
using System.Drawing;
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

	public GameWindow(uint width, uint height, string title)
	{
		_window = new RenderWindow(new VideoMode(new Vector2u(width, height)), title, Styles.Default, State.Windowed);
		_window.SetFramerateLimit(60);
		_window.Closed += (_, _) => _window.Close();
		_window.Resized += (_, _) => ApplyViewport();

		_viewport = new Rectangle(0, 0, (int)width, (int)height);
		ApplyViewport();
	}

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
			ApplyViewport();
		}
	}

	public void PollEvents() => _window.DispatchEvents();

	public void Clear(ColorF color) => _window.Clear(new SfColor(color.R, color.G, color.B, color.A));

	public void Present() => _window.Display();

	public void Dispose() => _window.Dispose();

	private void ApplyViewport()
	{
		var size = Size;
		if (size.Width <= 0 || size.Height <= 0) return;

		_window.SetView(new View(
			new Vector2f(_viewport.X + _viewport.Width / 2f, _viewport.Y + _viewport.Height / 2f),
			new Vector2f(_viewport.Width, _viewport.Height))
		{
			Viewport = new FloatRect(
				new Vector2f((float)_viewport.X / size.Width, (float)_viewport.Y / size.Height),
				new Vector2f((float)_viewport.Width / size.Width, (float)_viewport.Height / size.Height)),
		});
	}
}
