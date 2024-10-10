using Godot;

namespace EdgeOfPlain.Scr.Core.UnitControl;

public partial class SelectedRange : Control
{
	[Signal] public delegate void LeftSelectedEventHandler();
	[Signal] public delegate void RightSelectedEventHandler();
	public override void _Input(InputEvent @event)
	{
		if (@event is not InputEventMouseButton mouseButton) return;
		switch (mouseButton.ButtonIndex)
		{
			case MouseButton.Left:
			{
				if (!mouseButton.Pressed)
				{
					EmitSignal(SignalName.LeftSelected);
				}

				break;
			}
			case MouseButton.Right:
			{
				if (!mouseButton.Pressed)
				{
					EmitSignal(SignalName.RightSelected);
				}
			
				break;
			}
		}
	}
}