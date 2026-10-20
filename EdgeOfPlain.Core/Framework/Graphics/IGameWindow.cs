using System;
using System.Drawing;

namespace EdgeOfPlain.Core.Framework.Graphics;

public interface IGameWindow
{
	bool IsOpen { get; }

	Size Size { get; }

	Rectangle Viewport { get; set; }

	Camera2D Camera { get; set; }

	void ResetCamera();

	void PollEvents();

	void WaitForEvents(TimeSpan timeout);

	void ActivateContext();

	void DeactivateContext();

	void Clear(ColorF color);

	void Present();
}