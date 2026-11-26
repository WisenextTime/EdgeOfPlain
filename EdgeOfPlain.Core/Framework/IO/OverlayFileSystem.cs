using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zio;
using Zio.FileSystems;

namespace EdgeOfPlain.Core.Framework.IO
{
	public sealed class OverlayFileSystem : ReadOnlyFileSystem
	{
		public sealed record Layer(IFileSystem FileSystem, string Name);

		private readonly List<Layer> _layers = [];
		private readonly Dictionary<UPath, int> _shadows = new(UPathComparer.OrdinalIgnoreCase);
		private readonly List<string> _diagnostics = [];

		public OverlayFileSystem(IFileSystem baseContent, string name = "base")
			: this(new Layer(baseContent, name)) { }

		public OverlayFileSystem(Layer baseLayer)
			: base(baseLayer.FileSystem, false)
		{
			ArgumentNullException.ThrowIfNull(baseLayer);

			_layers.Add(baseLayer);
		}

		public Layer BaseLayer => _layers[0];

		public IReadOnlyList<Layer> Layers => _layers;

		public IReadOnlyDictionary<UPath, int> Shadows => _shadows;

		public IReadOnlyList<string> Diagnostics => _diagnostics;

		public bool IsSealed { get; private set; }

		public string Description => $"Overlay({string.Join(", ", _layers.Select(layer => layer.Name))})";

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

		public void Seal() => IsSealed = true;

		protected override bool FileExistsImpl(UPath path) => Target(path).FileExists(path);

		protected override bool DirectoryExistsImpl(UPath path) => _layers[0].FileSystem.DirectoryExists(path);

		protected override Stream OpenFileImpl(UPath path, FileMode mode, FileAccess access, FileShare share = FileShare.None)
		{
			if (mode != FileMode.Open || (access & FileAccess.Write) != 0) throw new IOException(FileSystemIsReadOnly);

			var target = Target(path);
			if (!target.FileExists(path))
				throw new FileNotFoundException($"Namespace '{BaseLayer.Name}' do not contain `{path}`", path.FullName);

			return target.OpenFile(path, mode, access, share);
		}

		protected override long GetFileLengthImpl(UPath path)
		{
			var target = Target(path);
			return !target.FileExists(path)
				? throw new FileNotFoundException($"Namespace '{BaseLayer.Name}' do not contain `{path}`", path.FullName)
				: target.GetFileLength(path);
		}

		protected override (IFileSystem FileSystem, UPath Path) ResolvePathImpl(UPath path) => (Target(path), path);

		private void IndexLayer(int index)
		{
			var layer = _layers[index];

			foreach (var path in EnumerateLayerFiles(layer.FileSystem))
			{
				if (!IsVisibleBelow(path))
				{
					_diagnostics.Add($"{path} from '{layer.Name}' is not found in low layers");
					continue;
				}

				if (_shadows.TryGetValue(path, out var previous))
					_diagnostics.Add($"{path} from '{layer.Name}' is simultaneously covered by '{_layers[previous].Name}'");

				_shadows[path] = index;
			}
		}

		private static IEnumerable<UPath> EnumerateLayerFiles(IFileSystem fileSystem)
			=> fileSystem.DirectoryExists(UPath.Root)
				? fileSystem.EnumerateFiles(UPath.Root, "*", SearchOption.AllDirectories)
				: [];

		private void RebuildIndex()
		{
			_shadows.Clear();
			_diagnostics.Clear();

			for (var index = 1; index < _layers.Count; index++) IndexLayer(index);
		}

		private IFileSystem Target(UPath path)
			=> _shadows.TryGetValue(path, out var index) ? _layers[index].FileSystem : _layers[0].FileSystem;

		private bool IsVisibleBelow(UPath path)
			=> _layers[0].FileSystem.FileExists(path) || _shadows.ContainsKey(path);
	}
}