using System;
using EdgeOfPlain.Core.Framework.IO;

namespace EdgeOfPlain.Core.Framework.Resource
{
	public static class CoreAssets
	{
		public const string BootstrapFolder = "Misc";

		public static AssemblyResourceFileSystem Content { get; } = new(
			typeof(CoreAssets).Assembly, include: path => !IsBootstrapPath(path));

		public static AssemblyResourceFileSystem Bootstrap { get; } = new(
			typeof(CoreAssets).Assembly, include: IsBootstrapPath);

		public static bool IsBootstrapPath(string path)
			=> path.Equals(BootstrapFolder, StringComparison.OrdinalIgnoreCase)
			   || path.StartsWith(BootstrapFolder + "/", StringComparison.OrdinalIgnoreCase);
	}
}