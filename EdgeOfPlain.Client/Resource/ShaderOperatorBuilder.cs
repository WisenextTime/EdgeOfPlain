using System;
using System.IO;
using EdgeOfPlain.Client.Utils;
using EdgeOfPlain.Core.Framework.Resource;
using SFML.Graphics;

namespace EdgeOfPlain.Client.Resource;

public static class ShaderOperatorBuilder
{
	private static readonly Func<Stream, Shader> _loader = stream => ShaderParser.ParseShader(stream);

	public static ResourceOperator<Shader> Build => field ??= new ResourceOperator<Shader>(_loader);
}