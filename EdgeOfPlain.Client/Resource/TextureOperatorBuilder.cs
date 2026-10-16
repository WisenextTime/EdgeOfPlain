using System;
using System.IO;
using EdgeOfPlain.Core.Framework.Resource;
using SFML.Graphics;

namespace EdgeOfPlain.Client.Resource;

public static class TextureOperatorBuilder
{
	private static readonly Func<Stream, Texture> _loader = stream => new Texture(stream, false);
	private static readonly Action<Stream, Texture> _saver = (stream, texture) =>
	{
		if (!texture.CopyToImage().SaveToMemory(out var buffer, "png")) throw new Exception("Save failed");
		stream.Write(buffer);
	};

	public static ResourceOperator<Texture> Build => field ??= new ResourceOperator<Texture>(_loader, _saver);
}
