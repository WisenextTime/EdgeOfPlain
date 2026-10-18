using System;
using System.Collections.Generic;
using System.Linq;
using EdgeOfPlain.Core.Framework.Events;
using EdgeOfPlain.Core.Framework.Graphics;
using EdgeOfPlain.Core.Framework.Input;
using EdgeOfPlain.Core.Framework.Time;

namespace EdgeOfPlain.Core.Framework.Scenes;

public sealed class SceneManager
{
	private readonly List<IScene> _scenes = [];
	private readonly List<SceneCommand> _pending = [];
	private SceneContext? _context;

	public IReadOnlyList<IScene> Scenes => _scenes;

	public IScene? Active => _scenes.Count == 0 ? null : _scenes[^1];

	public SceneContext Context => _context ?? throw new InvalidOperationException("Scene manager has no window attached yet.");

	public void Attach(IGameWindow window)
	{
		ArgumentNullException.ThrowIfNull(window);

		if (_context is { } attached)
		{
			if (!ReferenceEquals(attached.Window, window))
				throw new InvalidOperationException("The scene manager is already attached to another window.");
			return;
		}

		_context = new SceneContext(window, this);
	}

	public void Push(IScene scene)
	{
		ArgumentNullException.ThrowIfNull(scene);
		_pending.Add(new SceneCommand(SceneChangeKind.Push, scene));
	}

	public void Pop() => _pending.Add(new SceneCommand(SceneChangeKind.Pop, null));

	public void Replace(IScene scene)
	{
		ArgumentNullException.ThrowIfNull(scene);
		_pending.Add(new SceneCommand(SceneChangeKind.Replace, scene));
	}

	public void PopTo(IScene scene)
	{
		ArgumentNullException.ThrowIfNull(scene);
		_pending.Add(new SceneCommand(SceneChangeKind.PopTo, scene));
	}
	public bool HandleInput(IInputState input)
	{
		ArgumentNullException.ThrowIfNull(input);

		var handled = false;
		for (var index = _scenes.Count - 1; index >= 0; index--)
		{
			if (!_scenes[index].IsEnabled) continue;
			if (!_scenes[index].HandleInput(input)) continue;

			handled = true;
			break;
		}

		Flush();
		return handled;
	}

	public void Update(LogicTime time, IInputState input)
	{
		ArgumentNullException.ThrowIfNull(input);

		for (var index = _scenes.Count - 1; index >= 0; index--)
		{
			var scene = _scenes[index];
			if (!scene.IsEnabled) continue;

			scene.Update(time, input);
		}

		Flush();
	}

	public void Draw(IRenderPipeline pipeline, RenderTime time)
	{
		ArgumentNullException.ThrowIfNull(pipeline);

		foreach (var scene in _scenes.Where(scene => scene.IsVisible)) scene.Draw(pipeline, time);
		Flush();
	}

	public void Clear() => _pending.Add(new SceneCommand(SceneChangeKind.Clear, null));

	private void Flush()
	{
		foreach (var t in _pending) Apply(t);
		_pending.Clear();
	}

	private void Apply(in SceneCommand command)
	{
		var previous = Active;

		switch (command.Kind)
		{
			case SceneChangeKind.Push:
				Enter(command.Scene!, previous, SceneChangeKind.Push);
				break;

			case SceneChangeKind.Pop:
				if (previous is null) return;
				Exit(previous);
				EventBus.Publish(new SceneChangedEvent(SceneChangeKind.Pop, previous, Active));
				break;

			case SceneChangeKind.Replace:
				if (previous is not null) Exit(previous);
				Enter(command.Scene!, previous, SceneChangeKind.Replace);
				break;

			case SceneChangeKind.PopTo:
				var target = command.Scene!;
				var found = _scenes.IndexOf(target);
				if (found < 0) return;

				for (var index = _scenes.Count - 1; index > found; index--) Exit(_scenes[index]);
				EventBus.Publish(new SceneChangedEvent(SceneChangeKind.PopTo, previous, Active));
				break;

			case SceneChangeKind.Clear:
				while (_scenes.Count > 0) Exit(_scenes[^1]);
				EventBus.Publish(new SceneChangedEvent(SceneChangeKind.Clear, previous, null));
				break;
		}
	}

	private void Enter(IScene scene, IScene? previous, SceneChangeKind kind)
	{
		_scenes.Add(scene);

		try
		{
			scene.Enter(Context);
		}
		catch
		{
			_scenes.RemoveAt(_scenes.Count - 1);
			throw;
		}

		EventBus.Publish(new SceneChangedEvent(kind, previous, scene));
	}

	private void Exit(IScene scene)
	{
		_scenes.RemoveAt(_scenes.Count - 1);
		scene.Exit();
	}

	private readonly record struct SceneCommand(SceneChangeKind Kind, IScene? Scene);
}
