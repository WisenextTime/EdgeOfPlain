using System;
using System.Threading;
using EdgeOfPlain.Core.Framework.Events;
using EdgeOfPlain.Core.Framework.Graphics;
using EdgeOfPlain.Core.Framework.Input;
using EdgeOfPlain.Core.Framework.IO;
using EdgeOfPlain.Core.Framework.Mods;
using EdgeOfPlain.Core.Framework.Resource;
using EdgeOfPlain.Core.Framework.Scenes;
using EdgeOfPlain.Core.Framework.Time;

namespace EdgeOfPlain.Core;

public abstract class Launcher : IDisposable
{
	public const string AssetsHead = "assets";
	public const string DataHead = "data";
	public const string ModHead = "mod";

	private static readonly TimeSpan _eventPumpTimeout = TimeSpan.FromMilliseconds(200);
	private bool _initialized;
	private bool _disposed;

	protected abstract FileSystem BaseAssetsSystem { get; }
	protected abstract string DataDirectory { get; }
	protected abstract string ModDirectory { get; }

	protected ModFileSystem AssetFileSystem { get; } = new();
	protected ModLoader Mods { get; private set; } = null!;

	protected SceneManager Scenes { get; } = new();
	protected InputState Input { get; } = new();
	protected GameLoop Loop { get; private set; } = null!;

	protected void Init()
	{
		if (_initialized) throw new InvalidOperationException("The launcher has already been initialized.");
		_initialized = true;

		InitFileSystems();
		InitResourceOperators();
		LoadModData();
	}

	protected virtual void InitFileSystems()
	{
		AssetFileSystem.RegisterNamespace(ModFileSystem.BaseNamespace, BaseAssetsSystem);
		FileIO.RegisterFileSystem(AssetsHead, AssetFileSystem);
		FileIO.RegisterFileSystem(DataHead, new PhysicalFileSystem(DataDirectory));
		FileIO.RegisterFileSystem(ModHead, new PhysicalFileSystem(ModDirectory));
	}

	protected virtual void LoadModData()
	{
		Mods = new ModLoader(AssetFileSystem);
		Mods.Discover();
		Mods.Commit();
	}

	protected virtual void InitResourceOperators()
	{
		AssetsManager.RegisterResourceOperator(TextOperatorBuilder.Build);
	}

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;

		Mods?.Dispose();
		EventBus.Clear();
		AssetsManager.Clear();
		FileIO.Clear();
	}

	protected virtual ColorF ClearColor => ColorF.Black;

	protected virtual bool PumpsEventsInLoop => true;

	protected abstract IRenderPipeline CreatePipeline(IGameWindow window);

	protected abstract IScene CreateInitialScene();

	protected virtual ITimeSource CreateTimeSource() => new StopwatchTimeSource();

	protected void Launch(IGameWindow window)
	{
		ArgumentNullException.ThrowIfNull(window);

		Loop = new GameLoop(CreateTimeSource());
		Scenes.Attach(window);
		Scenes.Push(CreateInitialScene());

		var pipeline = CreatePipeline(window);
		var previousSize = window.Size;

		try
		{
			while (window.IsOpen)
			{
				Input.BeginFrame();

				if (PumpsEventsInLoop)
				{
					window.PollEvents();
					Input.ApplyPending();
				}

				var size = window.Size;
				if (size != previousSize)
				{
					EventBus.Publish(new WindowResizedEvent(previousSize, size));
					previousSize = size;
				}

				Loop.BeginFrame();

				Scenes.HandleInput(Input);
				while (Loop.TryStep(out var time)) Scenes.Update(time, Input);

				window.Clear(ClearColor);
				pipeline.Begin();
				Scenes.Draw(pipeline, Loop.RenderTime);
				pipeline.End();
				window.Present();
			}
		}
		finally
		{
			(pipeline as IDisposable)?.Dispose();
		}
	}

	protected void LaunchWithEventPump(IGameWindow window)
	{
		ArgumentNullException.ThrowIfNull(window);

		Exception? failure = null;
		var completed = 0;

		window.DeactivateContext();

		var game = new Thread(() =>
		{
			try
			{
				window.ActivateContext();
				Launch(window);
			}
			catch (Exception e)
			{
				failure = e;
			}
			finally
			{
				Volatile.Write(ref completed, 1);
			}
		})
		{
			IsBackground = true,
			Name = "game-loop",
		};
		game.Start();
		while (window.IsOpen && Volatile.Read(ref completed) == 0) window.WaitForEvents(_eventPumpTimeout);
		game.Join();

		if (failure is not null) throw new AggregateException("The game loop terminated with an error.", failure);
	}
}
