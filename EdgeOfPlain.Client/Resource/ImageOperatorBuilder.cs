using System;
using System.IO;
using EdgeOfPlain.Core.Framework.Resource;
using SFML.Graphics;

namespace EdgeOfPlain.Client.Resource;

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
