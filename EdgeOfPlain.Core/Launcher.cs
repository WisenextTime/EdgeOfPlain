using System;
using EdgeOfPlain.Core.Framework.Events;
using EdgeOfPlain.Core.Framework.Graphics;
using EdgeOfPlain.Core.Framework.Input;
using EdgeOfPlain.Core.Framework.IO;
using EdgeOfPlain.Core.Framework.Mods;
using EdgeOfPlain.Core.Framework.Resource;
using EdgeOfPlain.Core.Framework.Scenes;
using EdgeOfPlain.Core.Framework.Time;

namespace EdgeOfPlain.Core;

public abstract class Launcher
{
	public const string AssetsHead = "assets";
	public const string DataHead = "data";
	public const string ModHead = "mod";

	protected abstract FileSystem BaseAssetsSystem { get; }
	protected abstract string DataDirectory { get; }
	protected abstract string ModDirectory { get; }

	protected ModFileSystem AssetFileSystem { get; } = new();
	protected ModLoader Mods { get; private set; } = null!;

	/// <summary>事件总线：场景切换、窗口尺寸变化、地图加载等都从这里广播。</summary>
	public EventBus Events { get; } = new();

	public SceneManager Scenes { get; }

	/// <summary>宿主把平台事件喂给它，场景只读地查询。</summary>
	public InputState Input { get; } = new();

	/// <summary>固定步长循环，在 <see cref="Launch"/> 里创建。</summary>
	public GameLoop Loop { get; private set; } = null!;

	protected Launcher() => Scenes = new SceneManager(Events);

	protected void Init()
	{
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

	/// <summary>每帧绘制场景之前应用的清屏颜色。</summary>
	protected virtual ColorF ClearColor => ColorF.Black;

	/// <summary>宿主提供渲染管线实现；Core 只通过 <see cref="IRenderPipeline"/> 描述要画什么。</summary>
	protected abstract IRenderPipeline CreatePipeline(IGameWindow window);

	/// <summary>宿主提供栈底的第一个场景。</summary>
	protected abstract IScene CreateInitialScene();

	/// <summary>循环的时间源；测试或录制回放时覆写它就能精确驱动逻辑帧。</summary>
	protected virtual ITimeSource CreateTimeSource() => new StopwatchTimeSource();

	/// <summary>
	/// 主循环：逻辑固定步长、渲染可变帧率，两者在同一线程上交替执行。
	/// 每个渲染帧最多补跑若干个逻辑步，剩余零头作为 <see cref="RenderTime.Alpha"/> 交给渲染插值。
	/// </summary>
	protected void Launch(IGameWindow window)
	{
		ArgumentNullException.ThrowIfNull(window);

		Loop = new GameLoop(CreateTimeSource());
		Scenes.Attach(window);
		Scenes.Push(CreateInitialScene());

		var pipeline = CreatePipeline(window);
		var previousSize = window.Size;

		while (window.IsOpen)
		{
			Input.BeginFrame();
			window.PollEvents();

			var size = window.Size;
			if (size != previousSize)
			{
				Events.Publish(new WindowResizedEvent(previousSize, size));
				previousSize = size;
			}

			Loop.BeginFrame();

			// 输入每个渲染帧处理一次，而不是每个逻辑步：一个渲染帧里补跑多个逻辑步时，
			// "刚按下"不会被重复消费。
			Scenes.HandleInput(Input);
			while (Loop.TryStep(out var time)) Scenes.Update(time, Input);

			window.Clear(ClearColor);
			pipeline.Begin();
			Scenes.Draw(pipeline, Loop.RenderTime);
			pipeline.End();
			window.Present();
		}
	}
}
