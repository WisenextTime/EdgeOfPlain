using System;
using System.IO;
using EdgeOfPlain.Client.Utils;
using EdgeOfPlain.Core.Framework.Resource;
using SFML.Graphics;

namespace EdgeOfPlain.Client.Resource;

public static class FontOperatorBuilder
{
	private static readonly Func<Stream, Font> _loader = stream =>
	{
		using var buffer = new MemoryStream();
		stream.CopyTo(buffer);
		return new Font(buffer.ToArray());
	};
	public static ResourceOperator<Font> Build => field ??= new ResourceOperator<Font>(_loader);
}

public static class ImageOperatorBuilder
{
	private static readonly Func<Stream, Image> _loader = stream => new Image(stream);
	private static readonly Action<Stream, Image> _saver = (stream, image) =>
	{
		if (!image.SaveToMemory(out var buffer, "png")) throw new Exception("Save failed");
		stream.Write(buffer);
	};

	public static ResourceOperator<Image> Build => field ??= new ResourceOperator<Image>(_loader, _saver);
}

public static class ShaderOperatorBuilder
{
	public static ResourceOperator<Shader> Build => field ??= new ResourceOperator<Shader>(ShaderParser.ParseShader);
}

public static class TextureOperatorBuilder
{
	private static readonly Func<Stream, Texture> _loader = stream => new Texture(stream);
	private static readonly Action<Stream, Texture> _saver = (stream, texture) =>
	{
		if (!texture.CopyToImage().SaveToMemory(out var buffer, "png")) throw new Exception("Save failed");
		stream.Write(buffer);
	};

	public static ResourceOperator<Texture> Build => field ??= new ResourceOperator<Texture>(_loader, _saver);
}