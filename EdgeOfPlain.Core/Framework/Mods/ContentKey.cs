using System;
using System.Diagnostics.CodeAnalysis;

namespace EdgeOfPlain.Core.Framework.Mods;

public readonly record struct ContentKey(string ModId, string Type, string LocalId) : IComparable<ContentKey>
{
	public static bool TryParse(string text, [NotNullWhen(true)] out ContentKey? key)
	{
		key = null;

		if (string.IsNullOrWhiteSpace(text)) return false;

		var separator = text.LastIndexOf(':');
		var modId = string.Empty;
		var rest = text;

		if (separator >= 0)
		{
			modId = text[..separator];
			rest = text[(separator + 1)..];

			if (modId.Length == 0) return false;
		}

		var slash = rest.IndexOf('/');

		if (slash <= 0 || slash == rest.Length - 1) return false;

		var type = rest[..slash];
		var localId = rest[(slash + 1)..];

		if (type.Length == 0 || localId.Length == 0) return false;

		key = new ContentKey(modId, type, localId);
		return true;
	}

	public static ContentKey Parse(string text)
	{
		ArgumentNullException.ThrowIfNull(text);

		return TryParse(text, out var key)
			? key.Value
			: throw new FormatException($"'{text}' is not a content key (expected 'mod:type/id' or 'type/id').");
	}

	public override string ToString()
		=>  $"{ModId}:{Type}/{LocalId}";

	public int CompareTo(ContentKey other) => string.CompareOrdinal(ToString(), other.ToString());
}
