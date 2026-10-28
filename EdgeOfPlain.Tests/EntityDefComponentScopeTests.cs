using System;
using Arch.Core;
using Arch.Core.Extensions;
using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Framework.Mods;
using EdgeOfPlain.Core.Game.Components;
using Xunit;

namespace EdgeOfPlain.Tests;

[Collection("ComponentManager")]
public sealed class EntityDefComponentScopeTests
{
	private struct UnitMarker;

	private struct BuildingMarker;

	private struct EntityMarker;

	private struct SharedMarker;

	private struct UnitHealthBuff
	{
		public int Boost;

		public static UnitHealthBuff CreateComponent(UnitDef def) => new() { Boost = def.MaxHealth };
	}

	private struct BuildingCapacity
	{
		public int Capacity;

		public static BuildingCapacity CreateComponent(BuildingDef def) => new() { Capacity = def.MaxHealth };
	}

	private sealed class EliteUnitDef(string id) : UnitDef(id);

	private sealed class FakeMod(string id) : Mod
	{
		public override ModInfo ModInfo { get; } = new(id, new Version(1, 0, 0));
	}

	[Fact]
	public void DefTypeScopeAppliesOnlyToMatchingDefType()
	{
		ComponentManager.Reset();
		using var context = new ModContext(new FakeMod("moda"));
		context.AddComponent(_ => new UnitMarker(), ComponentScope<UnitDef>.ForDefType());
		context.AddComponent(_ => new BuildingMarker(), ComponentScope<BuildingDef>.ForDefType());
		ComponentManager.Seal();

		using var world = World.Create();
		var unit = ComponentManager.CreateEntity(world, new UnitDef("u"));
		var building = ComponentManager.CreateEntity(world, new BuildingDef("b"));

		Assert.True(unit.Has<UnitMarker>());
		Assert.False(unit.Has<BuildingMarker>());

		Assert.False(building.Has<UnitMarker>());
		Assert.True(building.Has<BuildingMarker>());
	}

	[Fact]
	public void EntityDefScopeAppliesToUnitsAndBuildings()
	{
		ComponentManager.Reset();
		using var context = new ModContext(new FakeMod("moda"));
		context.AddComponent(_ => new EntityMarker(), ComponentScope<EntityDef>.ForDefType());
		ComponentManager.Seal();

		using var world = World.Create();
		var unit = ComponentManager.CreateEntity(world, new UnitDef("u"));
		var building = ComponentManager.CreateEntity(world, new BuildingDef("b"));

		Assert.True(unit.Has<EntityMarker>());
		Assert.True(building.Has<EntityMarker>());
	}

	[Fact]
	public void UnscopedComponentAppliesToAllEntityDefs()
	{
		ComponentManager.Reset();
		using var context = new ModContext(new FakeMod("moda"));
		context.AddComponent(_ => new SharedMarker());
		ComponentManager.Seal();

		using var world = World.Create();
		var unit = ComponentManager.CreateEntity(world, new UnitDef("u"));
		var building = ComponentManager.CreateEntity(world, new BuildingDef("b"));

		Assert.True(unit.Has<SharedMarker>());
		Assert.True(building.Has<SharedMarker>());
	}

	[Fact]
	public void DefTypeScopeMatchesDerivedDefTypes()
	{
		ComponentManager.Reset();
		using var context = new ModContext(new FakeMod("moda"));
		context.AddComponent(_ => new UnitMarker(), ComponentScope<UnitDef>.ForDefType());
		ComponentManager.Seal();

		using var world = World.Create();
		var elite = ComponentManager.CreateEntity(world, new EliteUnitDef("e"));

		Assert.True(elite.Has<UnitMarker>());
	}

	[Fact]
	public void BuildingEntityReceivesBaseComponentsPlusScopedComponents()
	{
		ComponentManager.Reset();
		using var context = new ModContext(new FakeMod("moda"));
		context.AddComponent(HealthComponent.CreateComponent);
		context.AddComponent(TransformComponent.CreateComponent);
		context.AddComponent(_ => new BuildingMarker(), ComponentScope<BuildingDef>.ForDefType());
		ComponentManager.Seal();

		using var world = World.Create();
		var building = ComponentManager.CreateEntity(
			world,
			new BuildingDef("b") { MaxHealth = 80 });

		Assert.Equal(80, building.Get<HealthComponent>().MaxHealth);
		Assert.True(building.Has<TransformComponent>());
		Assert.True(building.Has<BuildingMarker>());
	}

	[Fact]
	public void UnitScopedFactoryReceivesTheUnitDef()
	{
		ComponentManager.Reset();
		using var context = new ModContext(new FakeMod("moda"));
		context.AddComponent(UnitHealthBuff.CreateComponent, ComponentScope<UnitDef>.ForDefType());
		ComponentManager.Seal();

		using var world = World.Create();
		var unit = ComponentManager.CreateEntity(
			world,
			new UnitDef("u") { MaxHealth = 90 });

		Assert.Equal(90, unit.Get<UnitHealthBuff>().Boost);
	}

	[Fact]
	public void BuildingScopedFactoryReceivesTheBuildingDef()
	{
		ComponentManager.Reset();
		using var context = new ModContext(new FakeMod("moda"));
		context.AddComponent(BuildingCapacity.CreateComponent, ComponentScope<BuildingDef>.ForDefType());
		ComponentManager.Seal();

		using var world = World.Create();
		var building = ComponentManager.CreateEntity(
			world,
			new BuildingDef("b") { MaxHealth = 100 });

		Assert.Equal(100, building.Get<BuildingCapacity>().Capacity);
	}

	[Fact]
	public void UnitScopedFactoryIsNotAppliedToBuildings()
	{
		ComponentManager.Reset();
		using var context = new ModContext(new FakeMod("moda"));
		context.AddComponent(UnitHealthBuff.CreateComponent, ComponentScope<UnitDef>.ForDefType());
		ComponentManager.Seal();

		using var world = World.Create();
		var building = ComponentManager.CreateEntity(
			world,
			new BuildingDef("b") { MaxHealth = 100 });

		Assert.False(building.Has<UnitHealthBuff>());
	}

	[Fact]
	public void UnitScopedVelocityComponentAppliesOnlyToUnits()
	{
		ComponentManager.Reset();
		using var context = new ModContext(new FakeMod("moda"));
		context.AddComponent(VelocityComponent.CreateComponent, ComponentScope<UnitDef>.ForDefType());
		ComponentManager.Seal();

		using var world = World.Create();
		var unit = ComponentManager.CreateEntity(world, new UnitDef("u") { MaxVelocity = 50f });
		var building = ComponentManager.CreateEntity(world, new BuildingDef("b"));

		Assert.Equal(50f, unit.Get<VelocityComponent>().MaxVelocity);
		Assert.False(building.Has<VelocityComponent>());
	}

	[Fact]
	public void ForDefTypeReturnsSameInstanceForSameType()
	{
		Assert.Same(ComponentScope<UnitDef>.ForDefType(), ComponentScope<UnitDef>.ForDefType());
		Assert.NotSame(ComponentScope<UnitDef>.ForDefType(), ComponentScope<BuildingDef>.ForDefType());
	}
}
