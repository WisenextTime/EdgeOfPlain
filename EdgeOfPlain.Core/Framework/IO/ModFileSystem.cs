using System;
using System.Collections.Generic;
using System.Linq;
using Zio;
using Zio.FileSystems;

namespace EdgeOfPlain.Core.Framework.IO;
public sealed class ModFileSystem() : ComposeFileSystem(new MountFileSystem(), owned: true)
{
	public const string BaseNamespace = "base";

	public static readonly IReadOnlySet<string> ReservedNamespaces =
		new HashSet<string>(StringComparer.OrdinalIgnoreCase) { BaseNamespace };

	private readonly Dictionary<string, OverlayFileSystem> _namespaces = new(StringComparer.OrdinalIgnoreCase);

	public bool IsSealed { get; private set; }

	public IReadOnlyDictionary<string, OverlayFileSystem> Namespaces => _namespaces;

	public string Description => "Mods";

	public static string NormalizeNamespace(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		var normalized = name.ToLowerInvariant();

		return normalized.Any(character => char.IsWhiteSpace(character) || character is '/' or '\\' or ':')
			? throw new ArgumentException(
				$"Namespace '{name}' cannot contain whitespace, '/', '\\' or ':'", nameof(name))
			: normalized;
	}

	public OverlayFileSystem RegisterNamespace(string name, IFileSystem content)
	{
		ArgumentNullException.ThrowIfNull(content);
		ObjectDisposedException.ThrowIf(IsDisposed, this);
		if (IsSealed) throw new InvalidOperationException("ResourceSystem has sealed");

		var normalized = NormalizeNamespace(name);
		var overlay = new OverlayFileSystem(content, normalized);
		if (!_namespaces.TryAdd(normalized, overlay))
			throw new InvalidOperationException($"Namespace '{normalized}' has already been registered");

		Mounts.Mount(ToMountPath(normalized), overlay);
		return overlay;
	}

	public bool UnregisterNamespace(string name)
	{
		ObjectDisposedException.ThrowIf(IsDisposed, this);
		if (IsSealed) throw new InvalidOperationException("ResourceSystem has sealed");

		if (!_namespaces.Remove(NormalizeNamespace(name), out var overlay)) return false;

		Mounts.Unmount(ToMountPath(name));
		overlay.Dispose();
		return true;
	}

	public bool AddOverrideLayer(string targetNamespace, IFileSystem layer, string layerName)
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

		return _namespaces.Values.Count(overlay => overlay.RemoveOverrideLayers(layerName));
	}

	public void Seal()
	{
		if (IsSealed) return;

		IsSealed = true;
		foreach (var overlay in _namespaces.Values) overlay.Seal();
	}

	protected override UPath ConvertPathToDelegate(UPath path)
	{
		var fullPath = path.FullName;
		if (fullPath.Length <= 1) return path;

		var separator = fullPath.IndexOf('/', 1);
		var name = separator < 0 ? fullPath[1..] : fullPath[1..separator];
		if (name.Length == 0) return path;

		var normalized = name.ToLowerInvariant();
		if (string.Equals(normalized, name, StringComparison.Ordinal)) return path;

		return separator < 0
			? new UPath("/" + normalized)
			: new UPath("/" + normalized + fullPath[separator..]);
	}

	protected override UPath ConvertPathFromDelegate(UPath path) => path;

	private static UPath ToMountPath(string name) => new("/" + NormalizeNamespace(name));

	private MountFileSystem Mounts => (MountFileSystem)Fallback!;
}