using System;
using System.IO;
using EdgeOfPlain.Core.Framework.Resource;
using SFML.Graphics;

namespace EdgeOfPlain.Client.Resource;

public static class TextureOperatorBuilder
{
	// sRGB stays off on purpose: neither RenderWindow nor RenderTexture is sRGB-capable, so an sRGB
	// texture would be sampled into linear space and stored raw, drawing every texture darker than authored.
	private static readonly Func<Stream, Texture> _loader = stream => new Texture(stream, false);
	private static readonly Action<Stream, Texture> _saver = (stream, texture) =>
	{
		if (!texture.CopyToImage().SaveToMemory(out var buffer, "png")) throw new Exception("Save failed");
		stream.Write(buffer);
	};

	public static ResourceOperator<Texture> Build => field ??= new ResourceOperator<Texture>(_loader, _saver);
}
