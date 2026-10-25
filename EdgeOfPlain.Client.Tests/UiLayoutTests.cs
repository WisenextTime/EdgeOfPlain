using System.Drawing;
using System.Numerics;
using EdgeOfPlain.Client.UI;
using EdgeOfPlain.Core.Framework.Input;
using Xunit;

namespace EdgeOfPlain.Client.Tests;

public sealed class UiLayoutTests
{
	private static UiButton AnchoredButton()
	{
		var button = new UiButton { Text = "+" };
		button.SetAnchored(new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(52f, 52f), new Vector2(1f, 1f));

		return button;
	}

	[Fact]
	public void FixedTopLeftElementKeepsOffsetPosition()
	{
		var element = new UiElement();
		element.SetAnchored(Vector2.Zero, new Vector2(12f, 20f), new Vector2(100f, 40f));

		var root = new UiRoot();
		root.Add(element);
		root.Resize(new Vector2(800f, 600f));

		Assert.Equal(new RectangleF(12f, 20f, 100f, 40f), element.Bounds);
	}

	[Fact]
	public void BottomRightAnchorFollowsViewportWidth()
	{
		var root = new UiRoot();
		var button = AnchoredButton();
		root.Add(button);

		root.Resize(new Vector2(800f, 600f));
		Assert.Equal(new RectangleF(732f, 532f, 52f, 52f), button.Bounds);

		root.Resize(new Vector2(1024f, 768f));
		Assert.Equal(new RectangleF(956f, 700f, 52f, 52f), button.Bounds);
	}

	[Fact]
	public void CenterAnchorWithPivotCentersElement()
	{
		var element = new UiElement();
		element.SetAnchored(new Vector2(0.5f, 0.5f), Vector2.Zero, new Vector2(200f, 80f), new Vector2(0.5f, 0.5f));

		var root = new UiRoot();
		root.Add(element);
		root.Resize(new Vector2(800f, 600f));

		Assert.Equal(new RectangleF(300f, 260f, 200f, 80f), element.Bounds);
	}

	[Fact]
	public void StretchAnchorsFillViewportWithMargin()
	{
		var element = new UiElement();
		element.SetStretch(new Vector2(10f, 20f));

		var root = new UiRoot();
		root.Add(element);
		root.Resize(new Vector2(800f, 600f));

		Assert.Equal(new RectangleF(10f, 20f, 780f, 560f), element.Bounds);
	}

	[Fact]
	public void ChildAnchorsAreRelativeToParentBounds()
	{
		var panel = new UiElement();
		panel.SetAnchored(new Vector2(0f, 0f), new Vector2(100f, 100f), new Vector2(200f, 120f));

		var child = new UiElement();
		child.SetAnchored(new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(40f, 30f), new Vector2(1f, 1f));

		panel.Add(child);

		var root = new UiRoot();
		root.Add(panel);
		root.Resize(new Vector2(800f, 600f));

		Assert.Equal(new RectangleF(100f, 100f, 200f, 120f), panel.Bounds);
		Assert.Equal(new RectangleF(252f, 182f, 40f, 30f), child.Bounds);
	}

	[Fact]
	public void HiddenElementDoesNotGetLaidOut()
	{
		var element = new UiElement { Visible = false };
		element.SetAnchored(Vector2.Zero, new Vector2(10f, 10f), new Vector2(50f, 50f));

		var root = new UiRoot();
		root.Add(element);
		root.Resize(new Vector2(400f, 300f));

		Assert.Equal(RectangleF.Empty, element.Bounds);
	}

	[Fact]
	public void ButtonRaisesClickWhenPressedInside()
	{
		var root = new UiRoot();
		var button = AnchoredButton();
		var clicks = 0;
		button.Clicked += _ => clicks++;
		root.Add(button);
		root.Resize(new Vector2(800f, 600f));

		var inside = new Vector2(760f, 560f);

		root.UpdatePointer(Pointer(inside, down: true, pressed: true, released: false));
		Assert.True(button.IsPressed);

		root.UpdatePointer(Pointer(inside, down: false, pressed: false, released: true));
		Assert.False(button.IsPressed);

		Assert.Equal(1, clicks);
	}

	[Fact]
	public void ButtonDoesNotClickWhenReleasedOutside()
	{
		var root = new UiRoot();
		var button = AnchoredButton();
		var clicks = 0;
		button.Clicked += _ => clicks++;
		root.Add(button);
		root.Resize(new Vector2(800f, 600f));

		root.UpdatePointer(Pointer(new Vector2(760f, 560f), down: true, pressed: true, released: false));
		root.UpdatePointer(Pointer(new Vector2(10f, 10f), down: false, pressed: false, released: true));

		Assert.Equal(0, clicks);
		Assert.False(button.IsPressed);
	}

	[Fact]
	public void HoverTracksPointerAndHidesWhenPointerLeaves()
	{
		var root = new UiRoot();
		var button = AnchoredButton();
		root.Add(button);
		root.Resize(new Vector2(800f, 600f));

		root.UpdatePointer(Pointer(new Vector2(760f, 560f), down: false, pressed: false, released: false));
		Assert.True(button.IsHovered);
		Assert.True(root.PointerOverUi);

		root.UpdatePointer(Pointer(new Vector2(20f, 20f), down: false, pressed: false, released: false));
		Assert.False(button.IsHovered);
		Assert.False(root.PointerOverUi);
	}

	[Fact]
	public void RootAcceptsInputState()
	{
		var root = new UiRoot();
		var button = AnchoredButton();
		var clicks = 0;
		button.Clicked += _ => clicks++;
		root.Add(button);
		root.Resize(new Vector2(800f, 600f));

		var input = new InputState();
		input.OnMouseMoved(760f, 560f);
		input.BeginFrame();
		input.OnMouseButtonDown(MouseButton.Left);
		root.Update(input);

		input.BeginFrame();
		input.OnMouseButtonUp(MouseButton.Left);
		root.Update(input);

		Assert.Equal(1, clicks);
	}

	private static PointerInput Pointer(Vector2 position, bool down, bool pressed, bool released)
		=> new(position, down, pressed, released);
}
