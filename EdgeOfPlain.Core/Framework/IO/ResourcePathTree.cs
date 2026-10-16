using System.Collections.Generic;

namespace EdgeOfPlain.Core.Framework.IO;

internal static class ResourcePathTree
{
	internal static void AddDirectoryChain(HashSet<string> directories, string path)
	{
		var index = path.LastIndexOf('/');
		while (index > 0)
		{
			var parent = path[..index];
			if (!directories.Add(parent)) return;

			index = parent.LastIndexOf('/');
		}
	}

	internal static void AddChild(Dictionary<string, List<FileSystemEntry>> children, string path, bool isDirectory)
	{
		var index = path.LastIndexOf('/');
		var parent = index < 0 ? string.Empty : path[..index];

		if (!children.TryGetValue(parent, out var siblings)) children[parent] = siblings = [];
		siblings.Add(new FileSystemEntry(path, isDirectory));
	}
}
