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
	private readonly List<IFrameSystem> _registeredFrameSystems = [];
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

	public void AddComponent<T>(Func<UnitDef, T> factory) where T : unmanaged
	{
		ObjectDisposedException.ThrowIf(IsDisposed, this);
		ArgumentNullException.ThrowIfNull(factory);

		ComponentManager.StageComponent(factory, SourceMod.ModInfo.Id);
		_registeredComponents.Add(typeof(T));
	}

	public void AddApplier<TCommand>(ICommandApplier<TCommand> applier) where TCommand : struct, ICommand
	{
		ObjectDisposedException.ThrowIf(IsDisposed, this);
		ArgumentNullException.ThrowIfNull(applier);

		CommandRegistry.RegisterApplier(applier);
		_registeredAppliers.Add(typeof(TCommand));
	}

	public void AddFrameSystem(IFrameSystem system)
	{
		ObjectDisposedException.ThrowIf(IsDisposed, this);
		ArgumentNullException.ThrowIfNull(system);

		CommandRegistry.RegisterFrameSystem(system);
		_registeredFrameSystems.Add(system);
	}

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

		foreach (var system in _registeredFrameSystems)
			CommandRegistry.TryUnregisterFrameSystem(system);
	}

	private void Clear()
	{
		_registeredComponents.Clear();
		_registeredDefs.Clear();
		_registeredAppliers.Clear();
		_registeredQueues.Clear();
		_registeredFrameSystems.Clear();
	}

	public void Dispose()
	{
		Clear();
		IsDisposed = true;
		GC.SuppressFinalize(this);
	}
}
