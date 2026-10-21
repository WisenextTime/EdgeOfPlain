using System;
using System.Drawing;
using EdgeOfPlain.Core.Framework.Graphics;

namespace EdgeOfPlain.Client.Tests;

internal sealed class FakeGameWindow : IGameWindow
{
	public bool IsOpen { get; set; } = true;

	public Size Size { get; set; } = new(1280, 720);

	public Rectangle Viewport { get; set; }

	public Camera2D Camera { get; set; } = Camera2D.Default;

	public void ResetCamera() => Camera = Camera2D.Default;

	public void PollEvents() { }

	public void WaitForEvents(TimeSpan timeout) { }

	public void ActivateContext() { }

	public void DeactivateContext() { }

	public void Clear(ColorF color) { }

	public void Present() { }
}
