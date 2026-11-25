using EdgeOfPlain.Core.Framework.IO;
using EdgeOfPlain.Core.Framework.Mods.Loading;
using Xunit;
using Zio.FileSystems;

namespace EdgeOfPlain.Tests;

public sealed class ModChecksumTests
{
	[Fact]
	public void DigestIsStableAndChangesWhenEffectiveAssetsChange()
	{
		using var first = new ModFileSystem();
		first.RegisterNamespace("base", new MemoryFileSystem().AddText("/z.txt", "last").AddText("/a.txt", "first"));
		using var second = new ModFileSystem();
		second.RegisterNamespace("base", new MemoryFileSystem().AddText("/a.txt", "first").AddText("/z.txt", "last"));

		var expected = ModChecksum.Compute(first, [], []);
		Assert.Equal(64, expected.Length);
		Assert.Equal(expected, ModChecksum.Compute(second, [], []));

		using var changed = new ModFileSystem();
		changed.RegisterNamespace("base", new MemoryFileSystem().AddText("/a.txt", "first").AddText("/z.txt", "changed"));
		Assert.NotEqual(expected, ModChecksum.Compute(changed, [], []));
	}
}