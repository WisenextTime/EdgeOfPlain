using System;
using System.Collections.Generic;
using Arch.Core;
using EdgeOfPlain.Core.Content;
using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Content.Systems;
using EdgeOfPlain.Core.Framework.Mods;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Game.Components;
using LiteNetLib.Utils;
using Xunit;

namespace EdgeOfPlain.Tests;

[Collection("ComponentManager")]
public sealed class CommandRegistryTests : IDisposable
{
	private readonly struct PingCommand(int value) : ICommand
	{
		public readonly int Value = value;
		public void Serialize(NetDataWriter writer) { }

		public void Deserialize(NetDataReader reader) { }
	}

	private sealed class PingApplier(List<int> sink) : ICommandApplier<PingCommand>
	{
		public void Apply(World world, in PingCommand command) => sink.Add(command.Value);
	}

	private sealed class TestMod : Mod
	{
		public override ModInfo ModInfo { get; } = new("testcmd", new Version(1, 0, 0));
	}

	public CommandRegistryTests()
	{
		CommandRegistry.Reset();

		if (ComponentManager.IsSealed) return;

		ComponentManager.StageComponent(HealthComponent.CreateComponent, "test");
		ComponentManager.StageComponent(TransformComponent.CreateComponent, "test");
		ComponentManager.Seal();
	}

	public void Dispose() => CommandRegistry.Reset();

	[Fact]
	public void ModContextRegistersAnApplierThatDispatchesCommands()
	{
		var seen = new List<int>();
		using var context = new ModContext(new TestMod());
		context.AddApplier(new PingApplier(seen));

		var bus = new CommandBus();
		var dispatcher = new CommandDispatcher(bus);
		using var world = World.Create();

		bus.Enqueue(new PingCommand(7));

		Assert.Equal(1, dispatcher.ApplyAll(world));
		Assert.Equal([7], seen);
		Assert.Empty(bus.PendingTypes);
	}

	[Fact]
	public void RegisteringASecondApplierForTheSameCommandThrows()
	{
		using var first = new ModContext(new TestMod());
		first.AddApplier(new PingApplier([]));

		using var second = new ModContext(new TestMod());

		var exception = Assert.Throws<InvalidOperationException>(() => second.AddApplier(new PingApplier([])));
		Assert.Contains("already registered", exception.Message);
	}

	[Fact]
	public void SealedRegistryRejectsFurtherRegistration()
	{
		CommandRegistry.Seal();

		using var context = new ModContext(new TestMod());

		Assert.True(CommandRegistry.IsSealed);
		Assert.Throws<InvalidOperationException>(() => context.AddApplier(new PingApplier([])));
	}

	[Fact]
	public void FailedModContextReturnsItsRegistrations()
	{
		var context = new ModContext(new TestMod());
		context.AddApplier(new PingApplier([]));

		Assert.Equal(1, CommandRegistry.ApplierCount);

		context.Return();

		Assert.Equal(0, CommandRegistry.ApplierCount);
		Assert.Null(CommandRegistry.FindApplier(typeof(PingCommand)));
	}

	[Fact]
	public void BaseModRegistersItsQueuesAndAppliers()
	{
		ComponentManager.Reset();
		CommandRegistry.Reset();

		var mod = new Base();
		using var context = new ModContext(mod);
		mod.InitMod(context);

		Assert.Equal(3, CommandRegistry.ApplierCount);
		Assert.NotNull(CommandRegistry.FindApplier(typeof(SpawnEntityCommand)));
		Assert.NotNull(CommandRegistry.FindApplier(typeof(DeleteEntityCommand)));
	}

	[Fact]
	public void ResetClearsAppliersQueuesAndSeal()
	{
		using var context = new ModContext(new TestMod());
		context.AddApplier(new PingApplier([]));
		CommandRegistry.Seal();

		CommandRegistry.Reset();

		Assert.False(CommandRegistry.IsSealed);
		Assert.Equal(0, CommandRegistry.ApplierCount);
	}
}