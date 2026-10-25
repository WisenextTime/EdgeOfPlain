using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using EdgeOfPlain.Core.Framework.Graphics;

namespace EdgeOfPlain.Client.UI;

public readonly record struct PointerInput(Vector2 Position, bool IsDown, bool Pressed, bool Released);

public class UiElement
{
	private readonly List<UiElement> _children = [];

	public string Name { get; set; } = string.Empty;

	public UiElement? Parent { get; private set; }

	public IReadOnlyList<UiElement> Children => _children;

	public Vector2 AnchorMin { get; set; }

	public Vector2 AnchorMax { get; set; }

	public Vector2 OffsetMin { get; set; }

	public Vector2 OffsetMax { get; set; }

	public Vector2 Pivot { get; set; }

	public RectangleF Bounds { get; private set; }

	public bool Visible { get; set; } = true;

	public bool Enabled { get; set; } = true;

	public bool IsHovered { get; private set; }

	public int Layer { get; set; }

	public Vector2 Position => new(Bounds.X, Bounds.Y);

	public Vector2 Size => new(Bounds.Width, Bounds.Height);

	protected virtual bool CapturesPointer => false;

	public void Add(UiElement child)
	{
		ArgumentNullException.ThrowIfNull(child);

		if (child == this)
			throw new ArgumentException("An element cannot be its own child.", nameof(child));

		child.Parent?._children.Remove(child);
		child.Parent = this;
		_children.Add(child);
	}

	public bool Remove(UiElement child)
	{
		ArgumentNullException.ThrowIfNull(child);

		if (!_children.Remove(child)) return false;

		child.Parent = null;

		return true;
	}

	public void Clear()
	{
		foreach (var child in _children) child.Parent = null;

		_children.Clear();
	}

	public void SetAnchors(Vector2 min, Vector2 max)
	{
		AnchorMin = min;
		AnchorMax = max;
	}

	public void SetOffsets(Vector2 min, Vector2 max)
	{
		OffsetMin = min;
		OffsetMax = max;
	}

	public void SetAnchored(Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
	{
		var resolvedPivot = pivot ?? Vector2.Zero;

		AnchorMin = anchor;
		AnchorMax = anchor;
		Pivot = resolvedPivot;
		OffsetMin = position - size * resolvedPivot;
		OffsetMax = OffsetMin + size;
	}

	public void SetStretch(Vector2 margin)
	{
		SetAnchors(Vector2.Zero, Vector2.One);
		SetOffsets(margin, -margin);
	}

	public void Layout(Vector2 viewport) => Layout(new RectangleF(0f, 0f, viewport.X, viewport.Y));

	public UiElement? Find(string name)
	{
		if (string.Equals(Name, name, StringComparison.Ordinal)) return this;

		foreach (var child in _children)
			if (child.Find(name) is { } found) return found;

		return null;
	}

	public TElement? Find<TElement>(string name) where TElement : UiElement => Find(name) as TElement;

	public void Layout(RectangleF parentBounds)
	{
		if (!Visible) return;

		Bounds = ResolveBounds(parentBounds);

		foreach (var child in _children) child.Layout(Bounds);

		OnLayout();
	}

	public bool Contains(Vector2 point) => Bounds.Contains(point.X, point.Y);

	public bool Update(in PointerInput pointer)
	{
		ResetHover();

		return Dispatch(pointer);
	}

	public void Draw(ISpriteBatch batch)
	{
		ArgumentNullException.ThrowIfNull(batch);

		if (!Visible) return;

		OnDraw(batch);

		foreach (var child in _children) child.Draw(batch);
	}

	protected virtual void OnLayout()
	{
	}

	protected virtual void OnDraw(ISpriteBatch batch)
	{
	}

	protected virtual bool OnPointer(in PointerInput pointer) => false;

	private void ResetHover()
	{
		IsHovered = false;

		foreach (var child in _children) child.ResetHover();
	}

	private bool Dispatch(in PointerInput pointer)
	{
		if (!Visible) return false;

		for (var index = _children.Count - 1; index >= 0; index--)
			if (_children[index].Dispatch(pointer)) return true;

		IsHovered = Enabled && Contains(pointer.Position);

		if (!IsHovered && !CapturesPointer) return false;

		return OnPointer(pointer);
	}

	private RectangleF ResolveBounds(RectangleF parentBounds)
	{
		var anchorMin = new Vector2(
			parentBounds.X + parentBounds.Width * AnchorMin.X,
			parentBounds.Y + parentBounds.Height * AnchorMin.Y);
		var anchorMax = new Vector2(
			parentBounds.X + parentBounds.Width * AnchorMax.X,
			parentBounds.Y + parentBounds.Height * AnchorMax.Y);

		var left = anchorMin.X + OffsetMin.X;
		var top = anchorMin.Y + OffsetMin.Y;
		var width = anchorMax.X + OffsetMax.X - left;
		var height = anchorMax.Y + OffsetMax.Y - top;

		return new RectangleF(left, top, MathF.Max(0f, width), MathF.Max(0f, height));
	}
}
