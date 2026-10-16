using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EdgeOfPlain.Core.Framework.IO;

public sealed class ModFileSystem : FileSystem
{
	public const string BaseNamespace = "base";

	public static readonly IReadOnlySet<string> ReservedNamespaces =
		new HashSet<string>(StringComparer.OrdinalIgnoreCase) { BaseNamespace };

	private readonly Dictionary<string, OverlayFileSystem> _namespaces = new(StringComparer.OrdinalIgnoreCase);

	public bool IsSealed { get; private set; }

	public IReadOnlyDictionary<string, OverlayFileSystem> Namespaces => _namespaces;

	public override bool IsCaseSensitive => false;

	public override bool IsReadOnly => true;

	public static string NormalizeNamespace(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		var normalized = name.ToLowerInvariant();
		return normalized.Any(character => char.IsWhiteSpace(character) || character is '/' or '\\' or ':')
			? throw new ArgumentException($"Namespace '{name}' cannot contain whitespace、'/'、'\\' or ':'", nameof(name))
			: normalized;

	}

	public OverlayFileSystem RegisterNamespace(string name, FileSystem content)
	{
		ArgumentNullException.ThrowIfNull(content);
		ObjectDisposedException.ThrowIf(IsDisposed, this);
		if (IsSealed) throw new InvalidOperationException("ResourceSystem has sealed");

		var normalized = NormalizeNamespace(name);
		var overlay = new OverlayFileSystem(content, normalized);
		return !_namespaces.TryAdd(normalized, overlay)
			? throw new InvalidOperationException($"Namespace '{normalized}' has already been registered")
			: overlay;

	}

	public bool UnregisterNamespace(string name)
	{
		ObjectDisposedException.ThrowIf(IsDisposed, this);
		if (IsSealed) throw new InvalidOperationException("ResourceSystem has sealed");

		if (!_namespaces.Remove(NormalizeNamespace(name), out var overlay)) return false;

		overlay.Dispose();
		return true;
	}

	public bool AddOverrideLayer(string targetNamespace, FileSystem layer, string layerName)
	{
		ArgumentNullException.ThrowIfNull(layer);
		ObjectDisposedException.ThrowIf(IsDisposed, this);
		if (IsSealed) throw new InvalidOperationException("ResourceSystem has sealed");

		if (!_namespaces.TryGetValue(NormalizeNamespace(targetNamespace), out var overlay)) return false;

		overlay.AddOverrideLayer(new OverlayFileSystem.Layer(layer, NormalizeNamespace(layerName)));
		return true;
	}

	public OverlayFileSystem? GetNamespace(string name) => _namespaces.GetValueOrDefault(NormalizeNamespace(name));
	public int RemoveOverrideLayers(string layerName)
	{
		ObjectDisposedException.ThrowIf(IsDisposed, this);
		if (IsSealed) throw new InvalidOperationException("ResourceSystem has sealed");

		var changed = 0;
		foreach (var overlay in _namespaces.Values)
			if (overlay.RemoveOverrideLayers(layerName)) changed++;

		return changed;
	}

	public void Seal()
	{
		if (IsSealed) return;

		IsSealed = true;
		foreach (var overlay in _namespaces.Values) overlay.Seal();
	}

	protected override bool FileExistsCore(string path)
		=> TryResolve(path, out var overlay, out var innerPath) && overlay.FileExists(innerPath);

	protected override bool DirectoryExistsCore(string path)
		=> TryResolve(path, out var overlay, out var innerPath) && overlay.DirectoryExists(innerPath);

	protected override Stream OpenReadCore(string path)
		=> Resolve(path, out var innerPath).OpenRead(innerPath);

	protected override IEnumerable<FileSystemEntry> EnumerateEntriesCore(string path)
		=> Resolve(path, out var innerPath).EnumerateEntries(innerPath);

	private OverlayFileSystem Resolve(string path, out string innerPath)
	{
		if (TryResolve(path, out var overlay, out innerPath)) return overlay;

		var separator = path.IndexOf('/');
		var name = separator < 0 ? path : path[..separator];
		throw name.Length == 0
			? new ArgumentException($"Path '{path}' do not contain a namespace", nameof(path))
			: new FileNotFoundException($"Namespace '{name}' is not found", path);
	}

	private bool TryResolve(string path, out OverlayFileSystem overlay, out string innerPath)
	{
		overlay = null!;
		innerPath = string.Empty;

		var separator = path.IndexOf('/');
		var name = separator < 0 ? path : path[..separator];
		if (name.Length == 0 || !_namespaces.TryGetValue(name, out var found)) return false;

		overlay = found;
		innerPath = separator < 0 ? string.Empty : path[(separator + 1)..];
		return true;
	}
}
