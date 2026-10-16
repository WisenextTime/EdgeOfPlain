using EdgeOfPlain.Core.Framework.Graphics;
using EdgeOfPlain.Core.Framework.IO;
using EdgeOfPlain.Core.Framework.Mods;
using EdgeOfPlain.Core.Framework.Resource;

namespace EdgeOfPlain.Core;

public abstract class Launcher
{
	public const string AssetsHead = "assets";
	public const string DataHead = "data";
	public const string ModHead = "mod";
	protected abstract FileSystem BaseAssetsSystem { get; }
	protected abstract string DataDirectory { get; }
	protected abstract string ModDirectory { get; }
	protected ModFileSystem AssetFileSystem { get; } = new();
	protected ModLoader Mods { get; private set; } = null!;

	protected void Init()
	{
		InitFileSystems();
		InitResourceOperators();
		LoadModData();
	}

	protected virtual void InitFileSystems()
	{
		AssetFileSystem.RegisterNamespace(ModFileSystem.BaseNamespace, BaseAssetsSystem);
		FileIO.RegisterFileSystem(AssetsHead, AssetFileSystem);
		FileIO.RegisterFileSystem(DataHead, new PhysicalFileSystem(DataDirectory));
		FileIO.RegisterFileSystem(ModHead, new PhysicalFileSystem(ModDirectory));
	}

	protected virtual void LoadModData()
	{
		Mods = new ModLoader(AssetFileSystem);
		Mods.Discover();
		Mods.Commit();
	}

	protected virtual void InitResourceOperators()
	{
		AssetsManager.RegisterResourceOperator(TextOperatorBuilder.Build);
	}

	protected void Launch(IGameWindow window)
	{
		
	}
}
