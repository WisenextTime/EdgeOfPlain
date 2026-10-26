using System;
using Arch.Core;
using Arch.Core.Extensions;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Framework.Mods;
using EdgeOfPlain.Core.Game.Components;
using Xunit;

namespace EdgeOfPlain.Tests;

[Collection("ComponentManager")]
public sealed class ModContextComponentTests
{
	private struct Marker
	{
		public int Value;
	}

	private struct OtherMarker
	{
		public int Value;
	}

	private sealed class FakeMod(string id) : Mod
	{
		public override ModInfo ModInfo { get; } = new(id, new Version(1, 0, 0));
	}

	[Fact]
	public void AddComponentRegistersTheFactoryUsedByCreateEntity()
	{
		ComponentManager.Reset();
		using var context = new ModContext(new FakeMod("moda"));

		context.AddComponent(_ => new Marker { Value = 42 });
		ComponentManager.Seal();

		using var world = World.Create();
		var entity = ComponentManager.CreateEntity(world, new UnitDef("unit"));

		Assert.Equal(42, entity.Get<Marker>().Value);
	}

	[Fact]
	public void StagedComponentIsOnlyCommittedAtSeal()
	{
		ComponentManager.Reset();
		using var context = new ModContext(new FakeMod("moda"));

		context.AddComponent(_ => new Marker());
		Assert.Equal(1, ComponentManager.PendingCount);

		ComponentManager.Seal();
		Assert.Equal(0, ComponentManager.PendingCount);
	}

	[Fact]
	public void FailedContextRemovesItsComponentBeforeSeal()
	{
		ComponentManager.Reset();

		var context = new ModContext(new FakeMod("moda"));
		context.AddComponent(_ => new Marker { Value = 1 });
		context.Return();

		Assert.Equal(0, ComponentManager.PendingCount);

		ComponentManager.Seal();

		using var world = World.Create();
		var entity = ComponentManager.CreateEntity(world, new UnitDef("unit"));

		Assert.False(entity.Has<Marker>());
	}

	[Fact]
	public void DuplicateComponentAcrossModsThrowsNamingTheOwner()
	{
		ComponentManager.Reset();
		using var first = new ModContext(new FakeMod("moda"));
		first.AddComponent(_ => new Marker());

		using var second = new ModContext(new FakeMod("modb"));

		var exception = Assert.Throws<InvalidOperationException>(() => second.AddComponent(_ => new Marker()));
		Assert.Contains("moda", exception.Message);
	}

	[Fact]
	public void DifferentComponentsFromDifferentModsCoexist()
	{
		ComponentManager.Reset();
		using var first = new ModContext(new FakeMod("moda"));
		using var second = new ModContext(new FakeMod("modb"));

		first.AddComponent(_ => new Marker { Value = 1 });
		second.AddComponent(_ => new OtherMarker { Value = 2 });
		ComponentManager.Seal();

		using var world = World.Create();
		var entity = ComponentManager.CreateEntity(world, new UnitDef("unit"));

		Assert.Equal(1, entity.Get<Marker>().Value);
		Assert.Equal(2, entity.Get<OtherMarker>().Value);
	}

	[Fact]
	public void SealedRegistryRejectsAddComponent()
	{
		ComponentManager.Reset();
		ComponentManager.Seal();

		using var context = new ModContext(new FakeMod("moda"));

		Assert.Throws<InvalidOperationException>(() => context.AddComponent(_ => new Marker()));
	}

	[Fact]
	public void UnstagingAfterSealIsRejected()
	{
		ComponentManager.Reset();
		using var context = new ModContext(new FakeMod("moda"));
		context.AddComponent(_ => new Marker());
		ComponentManager.Seal();

		Assert.False(ComponentManager.TryUnstageComponent(typeof(Marker)));
	}
}
