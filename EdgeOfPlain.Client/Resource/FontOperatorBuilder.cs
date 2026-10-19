using System;
using System.IO;
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
