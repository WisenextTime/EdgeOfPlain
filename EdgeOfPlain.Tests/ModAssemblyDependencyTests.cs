using System;
using System.IO;
using System.Linq;
using System.Reflection;
using EdgeOfPlain.Core.Framework.Mods;
using EdgeOfPlain.Core.Framework.Mods.Loading;
using Xunit;

namespace EdgeOfPlain.Tests
{
	public sealed class ModAssemblyDependencyTests
	{
		private static ModInfo Info(string id, params string[] dependencies)
			=> new(id, new Version(1, 0, 0),
				Dependence: dependencies.Select(dependency => new ModDependenceInfo(dependency, new Version(1, 0, 0)))
					.ToArray());

		[Fact]
		public void OrderInfosPlacesDependenciesBeforeTheirConsumers()
		{
			var alpha = Info("alpha");
			var beta = Info("beta", "alpha");

			var ordered = ModLoadOrder.OrderInfos([beta, alpha], static info => info);

			Assert.Equal(["alpha", "beta"], ordered.Select(info => info.Id));
		}

		[Fact]
		public void OrderInfosKeepsIndependentModsInIdOrder()
		{
			var ordered = ModLoadOrder.OrderInfos([Info("gamma"), Info("alpha"), Info("beta")], static info => info);

			Assert.Equal(["alpha", "beta", "gamma"], ordered.Select(info => info.Id));
		}

		[Fact]
		public void OrderInfosKeepsCyclicModsInIdOrder()
		{
			var first = Info("first", "second");
			var second = Info("second", "first");
			var independent = Info("independent");

			var ordered = ModLoadOrder.OrderInfos([second, first, independent], static info => info);

			Assert.Equal(["independent", "first", "second"], ordered.Select(info => info.Id));
		}

		[Fact]
		public void SharedContractAssembliesAreNeverResolvedLocally()
		{
			using var directory = new TempDirectory();
			using var consumerDirectory = new TempDirectory();
			var registry = new ModAssemblyRegistry();
			var consumer = new ModAssemblyLoadContext("consumer", consumerDirectory.Path, [], registry);
			registry.Add(consumer);

			try
			{
				Assert.Null(consumer.Resolve(new AssemblyName("EdgeOfPlain.Core")));
				Assert.Null(consumer.Resolve(new AssemblyName("Arch")));
			}
			finally
			{
				registry.Remove(consumer);
				consumer.Unload();
			}

			_ = directory;
		}

		[Fact]
		public void AssembliesAreResolvedFromTheModThatShipsThem()
		{
			using var providerDirectory = new TempDirectory();
			using var consumerDirectory = new TempDirectory();

			var shipped = Path.Combine(providerDirectory.Path, "EdgeOfPlain.Tests.dll");
			File.Copy(typeof(ModAssemblyDependencyTests).Assembly.Location, shipped);

			var registry = new ModAssemblyRegistry();
			var provider = new ModAssemblyLoadContext("provider", providerDirectory.Path, [shipped], registry);
			var consumer = new ModAssemblyLoadContext("consumer", consumerDirectory.Path, [], registry);
			registry.Add(provider);
			registry.Add(consumer);

			try
			{
				var name = new AssemblyName("EdgeOfPlain.Tests");
				var resolved = consumer.Resolve(name);

				Assert.NotNull(resolved);
				Assert.Same(provider.TryLoadOwn(name), resolved);
			}
			finally
			{
				registry.Remove(consumer);
				registry.Remove(provider);
				consumer.Unload();
				provider.Unload();
			}
		}

		[Fact]
		public void DeclaredDependenciesWinOverOtherModsShippingTheSameAssembly()
		{
			using var dependencyDirectory = new TempDirectory();
			using var otherDirectory = new TempDirectory();
			using var consumerDirectory = new TempDirectory();

			var shipped = Path.Combine(dependencyDirectory.Path, "EdgeOfPlain.Tests.dll");
			File.Copy(typeof(ModAssemblyDependencyTests).Assembly.Location, shipped);
			var duplicate = Path.Combine(otherDirectory.Path, "EdgeOfPlain.Tests.dll");
			File.Copy(typeof(ModAssemblyDependencyTests).Assembly.Location, duplicate);

			var registry = new ModAssemblyRegistry();
			var dependency = new ModAssemblyLoadContext("dependency", dependencyDirectory.Path, [shipped], registry);
			var other = new ModAssemblyLoadContext("other", otherDirectory.Path, [duplicate], registry);
			var consumer = new ModAssemblyLoadContext("consumer", consumerDirectory.Path, [], registry);
			registry.Add(other);
			registry.Add(dependency);
			registry.Add(consumer);
			consumer.AddProvider(dependency);

			try
			{
				var name = new AssemblyName("EdgeOfPlain.Tests");
				var resolved = consumer.Resolve(name);

				Assert.NotNull(resolved);
				Assert.Same(dependency.TryLoadOwn(name), resolved);
			}
			finally
			{
				registry.Remove(consumer);
				registry.Remove(dependency);
				registry.Remove(other);
				consumer.Unload();
				dependency.Unload();
				other.Unload();
			}
		}

		[Fact]
		public void RegistryForgetsAssembliesOfRemovedContexts()
		{
			using var directory = new TempDirectory();
			var shipped = Path.Combine(directory.Path, "EdgeOfPlain.Tests.dll");
			File.Copy(typeof(ModAssemblyDependencyTests).Assembly.Location, shipped);

			var registry = new ModAssemblyRegistry();
			var provider = new ModAssemblyLoadContext("provider", directory.Path, [shipped], registry);
			var requester = new ModAssemblyLoadContext("requester", directory.Path, [], registry);
			registry.Add(provider);

			Assert.True(registry.TryGetOwner("EdgeOfPlain.Tests", requester, out _));

			registry.Remove(provider);

			Assert.False(registry.TryGetOwner("EdgeOfPlain.Tests", requester, out _));

			requester.Unload();
			provider.Unload();
		}

		[Fact]
		public void RegistryKeepsModIdsForContextLookup()
		{
			using var directory = new TempDirectory();
			var registry = new ModAssemblyRegistry();
			var context = new ModAssemblyLoadContext("probe", directory.Path, [], registry);
			registry.Add(context);

			Assert.True(registry.TryGetContext("probe", out var found));
			Assert.Same(context, found);

			registry.Remove(context);
			Assert.False(registry.TryGetContext("probe", out _));

			context.Unload();
		}
	}
}