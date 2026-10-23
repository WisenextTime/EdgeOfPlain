using System;
using System.Collections.Generic;
using EdgeOfPlain.Core.Framework.Mods;
using EdgeOfPlain.Core.Utils;
using SFML.Graphics;

namespace EdgeOfPlain.Client.Resource.Shaders;

public static class ShaderLoader
{
	public const string DirectoryName = "Shaders";

	private static readonly List<string> _pending = [];
	private static readonly HashSet<string> _seen = new(StringComparer.Ordinal);
	private static int _next;

	public static int PendingCount => _pending.Count;

	public static bool Register() => ModResourceLoader.TryRegister(DirectoryName, Collect);

	public static bool WarmupNext(out int completed, out int total)
	{
		completed = _next;
		total = _pending.Count;

		if (_next >= _pending.Count) return false;

		var path = _pending[_next++];

		try
		{
			_ = ClientAssetsManager.Get<Shader>(path);
		}
		catch(Exception e)
		{
			LogUtils.LogMessage($"Failed to compile shader '{path}': {e.Message}", LogUtils.LogType.Error);
		}

		completed = _next;

		return true;
	}

	private static void Collect(ModResourceDirectory directory)
	{
		foreach (var file in directory.Files)
		{
			if (!_seen.Add(file)) continue;

			_pending.Add(file);
		}
	}
}
