using System;
using System.IO;
using Zio;
using Zio.FileSystems;

namespace EdgeOfPlain.Core.Framework.IO
{
	public static class ZioFileSystemExtensions
	{
		extension(IFileSystem fileSystem)
		{
			public Stream OpenReadShared(UPath path)
			{
				ArgumentNullException.ThrowIfNull(fileSystem);

				return fileSystem.OpenFile(path, FileMode.Open, FileAccess.Read, FileShare.Read);
			}

			public void CreateParentDirectory(UPath path)
			{
				ArgumentNullException.ThrowIfNull(fileSystem);

				var directory = path.GetDirectory();
				if (directory.IsNull || directory == UPath.Root) return;
				if (fileSystem.DirectoryExists(directory)) return;

				fileSystem.CreateDirectory(directory);
			}

			public bool TryGetPhysicalPath(UPath path, out string physicalPath)
			{
				ArgumentNullException.ThrowIfNull(fileSystem);

				physicalPath = null!;

				IFileSystem target;
				UPath innerPath;
				try
				{
					(target, innerPath) = fileSystem.ResolvePath(path);
				}
				catch(Exception e) when (e is ArgumentException or IOException or InvalidOperationException or NotSupportedException)
				{
					return false;
				}

				while (target is ComposeFileSystem { Fallback: { } fallback }) target = fallback;

				if (target is not PhysicalFileSystem physical) return false;

				try
				{
					physicalPath = physical.ConvertPathToInternal(innerPath);
					return true;
				}
				catch(ArgumentException)
				{
					return false;
				}
			}
		}

	}
}