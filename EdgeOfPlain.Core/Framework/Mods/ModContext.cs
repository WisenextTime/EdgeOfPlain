using System;
using System.Collections.Generic;
using EdgeOfPlain.Core.Definitions;

namespace EdgeOfPlain.Core.Framework.Mods;

public class ModContext : IDisposable
{
	private List<ContentDef> _registeredDefs = [];
	public bool IsDisposed { get; private set; }

	public ModContext(IMod sourceMod)
	{
		ArgumentNullException.ThrowIfNull(sourceMod);
		SourceMod = sourceMod;
	}

	private IMod SourceMod { get; }

	public void AddContent(ContentDef content)
	{
		ObjectDisposedException.ThrowIf(IsDisposed, this);
		ArgumentNullException.ThrowIfNull(content);
		content.SourceMod = SourceMod.ModInfo;
		ContentStore.Add(content);
		_registeredDefs.Add(content);
	}

	internal void Return()
	{
		foreach (var def in _registeredDefs)
			ContentStore.TryRemove(def);
	}

	private void Clear() => _registeredDefs.Clear();

	public void Dispose()
	{
		Clear();
		IsDisposed = true;
		GC.SuppressFinalize(this);
	}
}
