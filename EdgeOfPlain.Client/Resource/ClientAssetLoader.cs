using System;
using EdgeOfPlain.Core.Framework.Mods;
using EdgeOfPlain.Core.Utils;

namespace EdgeOfPlain.Client.Resource;

public static class ClientAssetLoader
{
	public static bool Register<T>(string directory, Func<string, bool>? filter = null) where T : class
		=> ModResourceLoader.TryRegister(directory, resources => LoadFolder<T>(resources, filter));

	public static void LoadFolder<T>(ModResourceDirectory directory, Func<string, bool>? filter = null) where T : class
	{
		foreach (var file in directory.Files)
		{
			if (filter is not null && !filter(file)) continue;

			try
			{
				ClientAssetsManager.Get<T>(file);
			}
			catch(Exception e)
			{
				LogUtils.LogMessage($"Failed to load client asset '{file}': {e.Message}", LogUtils.LogType.Error);
			}
		}
	}
}
