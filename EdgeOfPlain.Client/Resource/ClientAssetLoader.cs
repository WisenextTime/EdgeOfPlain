using System;
using EdgeOfPlain.Core.Framework.Mods;
using EdgeOfPlain.Core.Utils;
using Microsoft.Extensions.Logging;

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
				LogUtils.Logger(typeof(ClientAssetLoader))
					.LogError("Failed to load client asset '{File}': {EMessage}", file, e.Message);
			}
		}
	}
}