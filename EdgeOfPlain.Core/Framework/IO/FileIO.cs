using System;
using System.IO;
using System.Threading;
using Zio;
using Zio.FileSystems;

namespace EdgeOfPlain.Core.Framework.IO
{
	public static class FileIO
	{
		public static readonly UPath Assets = new("/assets");
		public static readonly UPath Data = new("/data");
		public static readonly UPath Mods = new("/mod");
		private static readonly Lock _gate = new();
		private static MountFileSystem? _root;

		public static bool IsInitialized
		{
			get
			{
				lock (_gate) return _root is not null;
			}
		}
		public static IFileSystem Root
		{
			get
			{
				lock (_gate)
				{
					return _root ?? throw new InvalidOperationException(
						"The game file system has not been initialized. Call FileIO.Initialize() first.");
				}
			}
		}

		public static void Initialize(string dataDirectory, string modDirectory)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
			ArgumentException.ThrowIfNullOrWhiteSpace(modDirectory);

			var physical = new PhysicalFileSystem();
			var root = new MountFileSystem();

			try
			{
				root.Mount(Data, CreateDirectoryMount(physical, dataDirectory));
				root.Mount(Mods, CreateDirectoryMount(physical, modDirectory));
			}
			catch
			{
				root.Dispose();
				throw;
			}

			lock (_gate)
			{
				if (_root is not null)
					throw new InvalidOperationException("The game file system has already been initialized.");

				_root = root;
			}
		}

		public static void Mount(UPath path, IFileSystem fileSystem)
		{
			ArgumentNullException.ThrowIfNull(fileSystem);

			GetMountTable().Mount(path, fileSystem);
		}

		public static IFileSystem Unmount(UPath path) => GetMountTable().Unmount(path);

		public static bool TryGetMounted(UPath path, out IFileSystem fileSystem)
		{
			if (GetMountTable().GetMounts().TryGetValue(path, out var mounted))
			{
				fileSystem = mounted;
				return true;
			}

			fileSystem = null!;
			return false;
		}

		private static MountFileSystem GetMountTable()
		{
			lock (_gate)
			{
				return _root ?? throw new InvalidOperationException(
					"The game file system has not been initialized. Call FileIO.Initialize() first.");
			}
		}

		public static void Reset()
		{
			MountFileSystem? root;
			lock (_gate)
			{
				root = _root;
				_root = null;
			}

			root?.Dispose();
		}

		private static SubFileSystem CreateDirectoryMount(IFileSystem physical, string directory)
		{
			var hostPath = Path.GetFullPath(directory);
			var mount = physical.ConvertPathFromInternal(hostPath);

			return physical.GetOrCreateSubFileSystem(mount);
		}
	}
}