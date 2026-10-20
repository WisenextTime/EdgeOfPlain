using System;
using System.Collections.Generic;
using System.IO;
using EdgeOfPlain.Core.Framework.Resource;
using SFML.Graphics;

namespace EdgeOfPlain.Client.Resource;

public sealed class UiFonts : IDisposable
{
	private readonly Dictionary<string, Font> _fonts = new(StringComparer.OrdinalIgnoreCase);

	public UiFonts()
	{
		Light = Load("Light", "Misc/Fonts/SourceHanSans-Light.otf");
		Bold = Load("Bold", "Misc/Fonts/SourceHanSansSC-Bold.otf");
	}

	public Font Light { get; }

	public Font Bold { get; }

	public bool TryGet(string name, out Font font) => _fonts.TryGetValue(name, out font!);

	public void Dispose()
	{
		foreach (var font in _fonts.Values) font.Dispose();
		_fonts.Clear();
	}

	private Font Load(string name, string path)
	{
		using var stream = CoreAssets.Bootstrap.OpenRead(path);
		using var buffer = new MemoryStream();
		stream.CopyTo(buffer);

		var font = new Font(buffer.ToArray());
		_fonts[name] = font;

		return font;
	}
}