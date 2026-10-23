using System;
using System.IO;
using System.Text;
using EdgeOfPlain.Client.Utils;
using Xunit;

namespace EdgeOfPlain.Client.Tests;

public sealed class ShaderParserTests
{
	[Fact]
	public void ParseSourcesSplitsStagesAndSharesPrelude()
	{
		const string text = """
		                    const vec4 Tint = vec4(1.0);

		                    //VERT
		                    void main() { gl_Position = vec4(0.0); }

		                    //FRAG
		                    void main() { gl_FragColor = Tint; }
		                    """;

		var sources = ShaderParser.ParseSources(Stream(text));

		Assert.Contains("const vec4 Tint", sources.Vertex);
		Assert.Contains("gl_Position", sources.Vertex);
		Assert.Contains("const vec4 Tint", sources.Fragment);
		Assert.Contains("gl_FragColor", sources.Fragment);
		Assert.Null(sources.Geometry);
	}

	[Fact]
	public void ParseSourcesHandlesGeometrySection()
	{
		const string text = """
		                    //VERT
		                    void main() { }

		                    //GEOM
		                    void main() { }

		                    //FRAG
		                    void main() { }
		                    """;

		var sources = ShaderParser.ParseSources(Stream(text));

		Assert.NotNull(sources.Vertex);
		Assert.NotNull(sources.Geometry);
		Assert.NotNull(sources.Fragment);
	}

	[Fact]
	public void ParseSourcesSkipsEmptyStages()
	{
		const string text = """
		                    //VERT
		                    void main() { }
		                    //FRAG
		                    """;

		var sources = ShaderParser.ParseSources(Stream(text));

		Assert.NotNull(sources.Vertex);
		Assert.Null(sources.Geometry);
		Assert.Null(sources.Fragment);
	}

	[Fact]
	public void ParseSourcesRejectsSourceWithoutStages()
	{
		Assert.Throws<InvalidDataException>(() => ShaderParser.ParseSources(Stream("float value = 1.0;")));
	}

	[Fact]
	public void ParseSourcesRejectsNullStream()
	{
		Assert.Throws<ArgumentNullException>(() => ShaderParser.ParseSources(null!));
	}

	private static MemoryStream Stream(string text) => new(Encoding.UTF8.GetBytes(text));
}
