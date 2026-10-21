using System;
using System.IO;
using System.Text;
using SFML.Graphics;

namespace EdgeOfPlain.Client.Utils;

public readonly record struct ShaderSources(string? Vertex, string? Geometry, string? Fragment);

public static class ShaderParser
{
	private enum Stage
	{
		Prelude,
		Vertex,
		Geometry,
		Fragment,
	}

	public static Shader ParseShader(Stream stream) => Compile(ParseSources(stream));

	public static ShaderSources ParseSources(Stream stream)
	{
		ArgumentNullException.ThrowIfNull(stream);

		var prelude = new StringBuilder();
		var vertex = new StringBuilder();
		var geometry = new StringBuilder();
		var fragment = new StringBuilder();

		var stage = Stage.Prelude;

		using var reader = new StreamReader(stream, Encoding.UTF8, true);

		while (reader.ReadLine() is { } line)
		{
			switch (line.Trim())
			{
				case "#VERT":
					stage = Stage.Vertex;
					continue;
				case "#GEOM":
					stage = Stage.Geometry;
					continue;
				case "#FRAG":
					stage = Stage.Fragment;
					continue;
			}

			BufferFor(stage, prelude, vertex, geometry, fragment).AppendLine(line);
		}

		var shared = prelude.ToString();
		var vertexSource = Compose(shared, vertex);
		var geometrySource = Compose(shared, geometry);
		var fragmentSource = Compose(shared, fragment);

		if (vertexSource is null && geometrySource is null && fragmentSource is null)
			throw new InvalidDataException("shader source has no #VERT, #GEOM or #FRAG section");

		return new ShaderSources(vertexSource, geometrySource, fragmentSource);
	}

	public static Shader Compile(ShaderSources sources)
		=> Shader.FromString(sources.Vertex, sources.Geometry, sources.Fragment);

	private static StringBuilder BufferFor(Stage stage, StringBuilder prelude, StringBuilder vertex, StringBuilder geometry,
		StringBuilder fragment)
		=> stage switch
		{
			Stage.Vertex => vertex,
			Stage.Geometry => geometry,
			Stage.Fragment => fragment,
			_ => prelude,
		};

	private static string? Compose(string shared, StringBuilder stage)
	{
		if (stage.Length == 0) return null;

		var body = stage.ToString();

		return body.Trim().Length == 0 ? null : shared + body;
	}
}