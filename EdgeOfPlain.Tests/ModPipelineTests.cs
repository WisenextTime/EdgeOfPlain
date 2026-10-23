using System;
using System.IO;
using System.Linq;
using System.Threading;
using EdgeOfPlain.Core;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Framework.IO;
using EdgeOfPlain.Core.Framework.Mods;
using EdgeOfPlain.Core.Framework.Mods.Loading;
using EdgeOfPlain.Core.Framework.Resource;
using Xunit;

namespace EdgeOfPlain.Tests;

public sealed class ModPipelineTests
{
	private static readonly Lock _gate = new();
	private static Pipeline? _pipeline;

	private static Pipeline Run()
	{
		lock (_gate)
		{
			return _pipeline ??= Execute();
		}
	}

	[Fact]
	public void LoadSucceedsWithoutError()
	{
		var pipeline = Run();

		Assert.Null(pipeline.Error);
		Assert.True(pipeline.IsLoaded);
	}

	[Fact]
	public void LoadsCoreModThenDependencyOrder()
	{
		var pipeline = Run();

		Assert.Equal(new[] { "base", "lib", "app" }, pipeline.LoadedIds);
	}

	[Fact]
	public void ContentOfLoadedModsIsAvailable()
	{
		var pipeline = Run();

		Assert.True(pipeline.HasGround);
		Assert.True(pipeline.HasLibSand);
		Assert.True(pipeline.HasAppStone);
	}

	[Fact]
	public void ContentOfFailedModIsRolledBack()
	{
		var pipeline = Run();

		Assert.False(pipeline.HasBadOre);
	}

	[Fact]
	public void TomlContentInContentsFolderIsLoaded()
	{
		var pipeline = Run();

		Assert.True(pipeline.HasBaseTomlContent);
	}

	[Fact]
	public void BuiltInContentTypesAreRegistered()
	{
		var pipeline = Run();

		Assert.True(pipeline.HasBuiltInContentTypes);
	}

	[Fact]
	public void RegistrationAfterLoadIsRejected()
	{
		var pipeline = Run();

		Assert.True(pipeline.RejectsLateRegistration);
	}

	[Fact]
	public void DependencyOnFailedModIsSkipped()
	{
		var pipeline = Run();

		Assert.DoesNotContain("needsbad", pipeline.LoadedIds);
		Assert.DoesNotContain("bad", pipeline.LoadedIds);
	}

	[Fact]
	public void DependencyCycleAndMissingDependencyAreNotLoaded()
	{
		var pipeline = Run();

		Assert.DoesNotContain("cyc1", pipeline.LoadedIds);
		Assert.DoesNotContain("cyc2", pipeline.LoadedIds);
		Assert.DoesNotContain("ghost", pipeline.LoadedIds);
		Assert.DoesNotContain("oldapp", pipeline.LoadedIds);
	}

	[Fact]
	public void ResourceLoaderReceivesOneDirectoryAtATime()
	{
		var pipeline = Run();

		Assert.Equal(1, pipeline.ProbeCalls);
		Assert.EndsWith("base/Probe/Tiles/", pipeline.ProbePath);
		Assert.Equal(2, pipeline.ProbeFiles.Length);
		Assert.All(pipeline.ProbeFiles, file => Assert.Contains("base/Probe/Tiles/", file));
	}

	private static Pipeline Execute()
	{
		var root = new TempDirectory();
		var baseDirectory = Path.Combine(root.Path, "base");
		var libDirectory = Path.Combine(root.Path, "lib");
		var appDirectory = Path.Combine(root.Path, "app");

		Directory.CreateDirectory(Path.Combine(baseDirectory, "Contents"));
		Directory.CreateDirectory(Path.Combine(libDirectory, "Contents"));
		Directory.CreateDirectory(Path.Combine(appDirectory, "Contents"));
		File.WriteAllText(Path.Combine(baseDirectory, "Contents", "base.toml"), """
		                                                                        type = "terrain"
		                                                                        id = "basedirt"
		                                                                        moveCost = 2
		                                                                        """);
		Directory.CreateDirectory(Path.Combine(baseDirectory, "Probe", "Tiles"));
		File.WriteAllText(Path.Combine(baseDirectory, "Probe", "Tiles", "a.txt"), "a");
		File.WriteAllText(Path.Combine(baseDirectory, "Probe", "Tiles", "b.txt"), "b");

		FileIO.Clear();
		ContentStore.Reset();
		ContentTypeRegistry.Reset();
		ContentSerializer.ResetOptions();
		AssetsManager.Clear();
		AssetsManager.RegisterResourceOperator(TextOperatorBuilder.Build);

		var assets = new ModFileSystem();
		assets.RegisterNamespace("base", new PhysicalFileSystem(baseDirectory));
		assets.RegisterNamespace("lib", new PhysicalFileSystem(libDirectory));
		assets.RegisterNamespace("app", new PhysicalFileSystem(appDirectory));
		FileIO.RegisterFileSystem(Launcher.AssetsHead, assets);

		Register(new TestMod(Info("lib"), init: context => context.AddContent(new TerrainDef("libsand"))));
		Register(new TestMod(Info("app", ("lib", "1.0.0")),
			init: context => context.AddContent(new TerrainDef("appstone"))));
		Register(new TestMod(Info("bad"), init: context =>
		{
			context.AddContent(new TerrainDef("badore"));
			throw new InvalidOperationException("boom");
		}));
		Register(new TestMod(Info("needsbad", ("bad", "1.0.0"))));
		Register(new TestMod(Info("oldapp", ("lib", "2.0.0"))));
		Register(new TestMod(Info("ghost", ("missing", "1.0.0"))));
		Register(new TestMod(Info("cyc1", ("cyc2", "1.0.0"))));
		Register(new TestMod(Info("cyc2", ("cyc1", "1.0.0"))));

		var probeCalls = 0;
		string? probePath = null;
		string[] probeFiles = [];
		ModResourceLoader.TryRegister("Probe", directory =>
		{
			probeCalls++;
			probePath = directory.Path;
			probeFiles = [.. directory.Files];
		});

		Exception? error = null;
		try
		{
			ModLoader.LoadRegistered();
		}
		catch(Exception exception)
		{
			error = exception;
		}

		var lateRejected = false;
		try
		{
			Register(new TestMod(Info("late")));
		}
		catch(Exception)
		{
			lateRejected = true;
		}

		return new Pipeline(
			[.. ModLoader.LoadedMods.Select(mod => mod.ModInfo.Id)],
			ModLoader.IsLoaded,
			error,
			ContentStore.Get("ground") is not null,
			ContentStore.Get("libsand") is not null,
			ContentStore.Get("appstone") is not null,
			ContentStore.Get("badore") is not null,
			ContentStore.Get("basedirt") is not null,
			lateRejected,
			ContentTypeRegistry.ByType.ContainsKey(typeof(TilesetDef))
			&& ContentTypeRegistry.ByType.ContainsKey(typeof(TileRendererDef)),
			probeCalls,
			probePath,
			probeFiles);
	}

	private static void Register(Mod mod) => ModRegistry.Register(mod, mod.ModInfo);

	private static ModInfo Info(string id, params (string Id, string MinVersion)[] dependencies)
		=> new(
			id,
			new Version(1, 0, 0),
			"tests",
			"",
			[.. dependencies.Select(dependency => new ModDependenceInfo(dependency.Id, Version.Parse(dependency.MinVersion)))]);

	private sealed record Pipeline(
		string[] LoadedIds,
		bool IsLoaded,
		Exception? Error,
		bool HasGround,
		bool HasLibSand,
		bool HasAppStone,
		bool HasBadOre,
		bool HasBaseTomlContent,
		bool RejectsLateRegistration,
		bool HasBuiltInContentTypes,
		int ProbeCalls,
		string? ProbePath,
		string[] ProbeFiles);

	private sealed class TestMod(ModInfo info, Type[]? types = null, Action<ModContext>? init = null) : Mod
	{
		public override ModInfo ModInfo => info;

		public override Type[]? RegisterTypes { get; protected set; } = types;

		public override void InitMod(ModContext context) => init?.Invoke(context);
	}
}