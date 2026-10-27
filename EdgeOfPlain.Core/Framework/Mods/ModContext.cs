using System;
using System.Collections.Generic;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Game.Components;
using EdgeOfPlain.Core.Game.Systems;

namespace EdgeOfPlain.Core.Framework.Mods;

public class ModContext : IDisposable
{
	private readonly List<Type> _registeredComponents = [];
	private readonly List<ContentDef> _registeredDefs = [];
	private readonly List<Type> _registeredAppliers = [];
	private readonly List<IQueueSystem> _registeredQueues = [];
	public bool IsDisposed { get; private set; }

	public ModContext(Mod sourceMod)
	{
		ArgumentNullException.ThrowIfNull(sourceMod);
		SourceMod = sourceMod;
	}

	private Mod SourceMod { get; }

	public void AddContent(ContentDef content)
	{
		ObjectDisposedException.ThrowIf(IsDisposed, this);
		ArgumentNullException.ThrowIfNull(content);
		content.SourceMod = SourceMod.ModInfo;
		ContentStore.Add(content);
		_registeredDefs.Add(content);
	}

	/// <summary>注册一个单位组件；同一组件类型只能注册一次，mod 加载完成后不再接受注册。</summary>
	public void AddComponent<T>(Func<UnitDef, T> factory) where T : unmanaged
	{
		ObjectDisposedException.ThrowIf(IsDisposed, this);
		ArgumentNullException.ThrowIfNull(factory);

		ComponentManager.StageComponent(factory, SourceMod.ModInfo.Id);
		_registeredComponents.Add(typeof(T));
	}

	/// <summary>注册一条命令的处理器；同一命令类型只能注册一次，mod 加载完成后不再接受注册。</summary>
	public void AddApplier<TCommand>(ICommandApplier<TCommand> applier) where TCommand : struct, ICommand
	{
		ObjectDisposedException.ThrowIf(IsDisposed, this);
		ArgumentNullException.ThrowIfNull(applier);

		CommandRegistry.RegisterApplier(applier);
		_registeredAppliers.Add(typeof(TCommand));
	}

	/// <summary>注册一个队列系统；每帧由 <c>GameWorld.RunQueues</c> 依次执行，用来把组件状态翻译成命令。</summary>
	public void AddQueue(IQueueSystem queue)
	{
		ObjectDisposedException.ThrowIf(IsDisposed, this);
		ArgumentNullException.ThrowIfNull(queue);

		CommandRegistry.RegisterQueue(queue);
		_registeredQueues.Add(queue);
	}

	internal void Return()
	{
		foreach (var component in _registeredComponents)
			ComponentManager.TryUnstageComponent(component);

		foreach (var def in _registeredDefs)
			ContentStore.TryRemove(def);

		foreach (var applier in _registeredAppliers)
			CommandRegistry.TryUnregisterApplier(applier);

		foreach (var queue in _registeredQueues)
			CommandRegistry.TryUnregisterQueue(queue);
	}

	private void Clear()
	{
		_registeredComponents.Clear();
		_registeredDefs.Clear();
		_registeredAppliers.Clear();
		_registeredQueues.Clear();
	}

	public void Dispose()
	{
		Clear();
		IsDisposed = true;
		GC.SuppressFinalize(this);
	}
}
