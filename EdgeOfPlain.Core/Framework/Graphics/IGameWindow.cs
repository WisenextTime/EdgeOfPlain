using System.Drawing;

namespace EdgeOfPlain.Core.Framework.Graphics;

public interface IGameWindow
{
	bool IsOpen { get; }

	Size Size { get; }

	Rectangle Viewport { get; set; }

	void PollEvents();

	void Clear(ColorF color);

	void Present();
}
