using System;
using EdgeOfPlain.Client.Resource;
using Xunit;

namespace EdgeOfPlain.Client.Tests;

public sealed class ClientAssetStoreTests
{
	private sealed class First { }
	private sealed class Second { }

	[Fact]
	public void SetAndTryGetKeepTypesSeparate()
	{
		var store = new ClientAssetStore();
		var first = new First();
		var second = new Second();

		store.Set("same", first);
		store.Set("same", second);

		Assert.True(store.TryGet("same", out First storedFirst));
		Assert.Same(first, storedFirst);
		Assert.True(store.TryGet("same", out Second storedSecond));
		Assert.Same(second, storedSecond);
	}

	[Fact]
	public void TryGetReturnsFalseForMissingKey()
	{
		var store = new ClientAssetStore();

		Assert.False(store.TryGet("missing", out First value));
		Assert.Null(value);
	}

	[Fact]
	public void AllReturnsEveryEntryOfThatType()
	{
		var store = new ClientAssetStore();
		store.Set("a", new First());
		store.Set("b", new First());
		store.Set("c", new Second());

		Assert.Equal(2, store.All<First>().Count);
		Assert.Single(store.All<Second>());
	}

	[Fact]
	public void ValuesContainsStoredObjects()
	{
		var store = new ClientAssetStore();
		var first = new First();
		store.Set("a", first);

		Assert.Contains(store.Values(), value => ReferenceEquals(value, first));
	}

	[Fact]
	public void ClearRemovesEverything()
	{
		var store = new ClientAssetStore();
		store.Set("a", new First());

		store.Clear();

		Assert.Empty(store.All<First>());
		Assert.Empty(store.Values());
	}

	[Fact]
	public void SetRejectsBlankKeyAndNullValue()
	{
		var store = new ClientAssetStore();

		Assert.Throws<ArgumentException>(() => store.Set("", new First()));
		Assert.Throws<ArgumentNullException>(() => store.Set("a", (First)null!));
	}
}
