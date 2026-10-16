using System;
using System.Collections.Generic;
using System.IO;

namespace EdgeOfPlain.Core.Framework.IO;

public sealed class OverlayFileSystem : FileSystem
{
	public sealed record Layer(FileSystem FileSystem, string Name);

	private readonly List<Layer> _layers = [];
	private readonly Dictionary<string, int> _shadows;
	private readonly List<string> _diagnostics = [];

	public OverlayFileSystem(FileSystem baseContent, string name = "base") : this(new Layer(baseContent, name)) { }

	public OverlayFileSystem(Layer baseLayer)
	{
		ArgumentNullException.ThrowIfNull(baseLayer);

		_layers.Add(baseLayer);
		_shadows = new Dictionary<string, int>(baseLayer.FileSystem.PathComparer);
	}

	public Layer BaseLayer => _layers[0];

	public IReadOnlyList<Layer> Layers => _layers;

	public IReadOnlyDictionary<string, int> Shadows => _shadows;

	public IReadOnlyList<string> Diagnostics => _diagnostics;

	public bool IsSealed { get; private set; }

	public override bool IsReadOnly => true;

	public override bool IsCaseSensitive => _layers[0].FileSystem.IsCaseSensitive;

	public void AddOverrideLayer(Layer layer)
	{
		ArgumentNullException.ThrowIfNull(layer);
		ObjectDisposedException.ThrowIf(IsDisposed, this);
		if (IsSealed) throw new InvalidOperationException("ResourceSystem has sealed");

		_layers.Add(layer);
		IndexLayer(_layers.Count - 1);
	}

	public bool RemoveOverrideLayers(string layerName)
	{
		ObjectDisposedException.ThrowIf(IsDisposed, this);
		if (IsSealed) throw new InvalidOperationException("ResourceSystem has sealed");

		var removed = 0;
		for (var index = _layers.Count - 1; index >= 1; index--)
		{
			if (!string.Equals(_layers[index].Name, layerName, StringComparison.OrdinalIgnoreCase)) continue;
			_layers.RemoveAt(index);
			removed++;
		}

		if (removed > 0) RebuildIndex();
		return removed > 0;
	}

	private void IndexLayer(int index)
	{
		var layer = _layers[index];

		foreach (var path in layer.FileSystem.EnumerateFiles(string.Empty, "*", SearchOption.AllDirectories))
		{
			if (!IsVisibleBelow(path))
			{
				_diagnostics.Add(
					$"{path} from '{layer.Name}' is not found in low layers");
				continue;
			}

			if (_shadows.TryGetValue(path, out var previous))
				_diagnostics.Add($"{path} from '{layer.Name}' is simultaneously covered by '{_layers[previous].Name}'");

			_shadows[path] = index;
		}
	}

	private void RebuildIndex()
	{
		_shadows.Clear();
		_diagnostics.Clear();

		for (var index = 1; index < _layers.Count; index++) IndexLayer(index);
	}

	public void Seal() => IsSealed = true;

	protected override bool FileExistsCore(string path) => Target(path).FileExists(path);
	protected override bool DirectoryExistsCore(string path) => _layers[0].FileSystem.DirectoryExists(path);

	protected override Stream OpenReadCore(string path)
	{
		var target = Target(path);
		if (!target.FileExists(path))
			throw new FileNotFoundException($"Namespace '{BaseLayer.Name}' do not contain '{path}'", path);

		return target.OpenRead(path);
	}

	protected override IEnumerable<FileSystemEntry> EnumerateEntriesCore(string path)
		=> _layers[0].FileSystem.EnumerateEntries(path);

	protected override void Dispose(bool disposing) { }

	private FileSystem Target(string path)
		=> _shadows.TryGetValue(path, out var index) ? _layers[index].FileSystem : _layers[0].FileSystem;

	private bool IsVisibleBelow(string path)
		=> _layers[0].FileSystem.FileExists(path) || _shadows.ContainsKey(path);
}
