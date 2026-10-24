using System;
using System.IO;
using System.Text.Json;
using EdgeOfPlain.Core.Definitions.Worlds;
using Tomlyn;

namespace EdgeOfPlain.Core.Framework.Resource;

public static class TextOperatorBuilder
{
	private static readonly Func<Stream, string> _loader = stream =>
	{
		using var reader = new StreamReader(stream);
		return reader.ReadToEnd();
	};
	private static readonly Action<Stream, string> _saver = (stream, text) =>
	{
		using var writer = new StreamWriter(stream);
		writer.Write(text);
	};
	public static ResourceOperator<string> Build => field ??= new ResourceOperator<string>(_loader, _saver);
}

public static class TextureAtlasOperatorBuilder
{
	private static readonly Func<Stream, TextureAtlas> _loader = stream =>
	{
		using var reader = new StreamReader(stream);
		return TextureAtlas.Parse(reader.ReadToEnd());
	};

	public static ResourceOperator<TextureAtlas> Build => field ??= new ResourceOperator<TextureAtlas>(_loader);
}

public static class MapOperatorBuilder
{
	private static readonly TomlSerializerOptions _options = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
	};

	private static readonly Func<Stream, Map> _loader = stream =>
	{
		using var reader = new StreamReader(stream);
		return TomlSerializer.Deserialize<Map>(reader.ReadToEnd(), _options);
	};

	public static ResourceOperator<Map> Build => field ??= new ResourceOperator<Map>(_loader);
}