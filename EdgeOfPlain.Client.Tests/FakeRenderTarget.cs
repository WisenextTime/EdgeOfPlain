using System.Collections.Generic;
using SFML.Graphics;
using SFML.System;

namespace EdgeOfPlain.Client.Tests;

internal sealed class FakeRenderTarget : IRenderTarget
{
	private View _view = new(new FloatRect(new Vector2f(0, 0), new Vector2f(1280, 720)));

	public Vector2u Size { get; set; } = new(1280, 720);

	public bool IsSrgb => false;

	public View DefaultView => _view;

	public List<DrawBatch> VertexBatches { get; } = [];

	public List<IDrawable> Drawables { get; } = [];

	public View GetView() => _view;

	public void SetView(View view) => _view = view;

	public IntRect GetViewport(View view)
	{
		var viewport = view.Viewport;
		return new IntRect(
			new Vector2i((int)viewport.Left, (int)viewport.Top),
			new Vector2i((int)viewport.Width, (int)viewport.Height));
	}

	public IntRect GetScissor(View view)
	{
		var scissor = view.Scissor;
		return new IntRect(
			new Vector2i((int)scissor.Left, (int)scissor.Top),
			new Vector2i((int)scissor.Width, (int)scissor.Height));
	}

	public Vector2f MapPixelToCoords(Vector2i point) => new(point.X, point.Y);

	public Vector2f MapPixelToCoords(Vector2i point, View view) => new(point.X, point.Y);

	public Vector2i MapCoordsToPixel(Vector2f point) => new((int)point.X, (int)point.Y);

	public Vector2i MapCoordsToPixel(Vector2f point, View view) => new((int)point.X, (int)point.Y);

	public void Clear() { }

	public void Clear(Color color) { }

	public void Clear(Color color, StencilValue stencilValue) { }

	public void ClearStencil(StencilValue stencilValue) { }

	public void Draw(IDrawable drawable) => Drawables.Add(drawable);

	public void Draw(IDrawable drawable, RenderStates states) => Drawables.Add(drawable);

	public void Draw(Vertex[] vertices, PrimitiveType type)
		=> Draw(vertices, 0, (uint)vertices.Length, type, RenderStates.Default);

	public void Draw(Vertex[] vertices, PrimitiveType type, RenderStates states)
		=> Draw(vertices, 0, (uint)vertices.Length, type, states);

	public void Draw(Vertex[] vertices, uint start, uint count, PrimitiveType type)
		=> Draw(vertices, start, count, type, RenderStates.Default);

	public void Draw(Vertex[] vertices, uint start, uint count, PrimitiveType type, RenderStates states)
		=> VertexBatches.Add(new DrawBatch(vertices[(int)start..(int)(start + count)], type, states));

	public bool SetActive(bool active) => true;

	public void PushGLStates() { }

	public void PopGLStates() { }

	public void ResetGLStates() { }

	internal sealed record DrawBatch(Vertex[] Vertices, PrimitiveType Type, RenderStates States)
	{
		public Vertex First => Vertices[0];

		public Vertex Last => Vertices[^1];
	}
}
