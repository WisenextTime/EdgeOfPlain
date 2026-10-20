using System;
using System.IO;

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