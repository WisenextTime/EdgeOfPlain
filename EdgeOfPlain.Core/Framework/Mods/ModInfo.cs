namespace EdgeOfPlain.Core.Framework.Mods;

/// <summary>
/// Who declared a piece of content. Runtime provenance only: it never reaches a content file,
/// because a definition's origin depends on what happens to be installed.
/// </summary>
public readonly record struct ModInfo(string Id, int Order)
{
	/// <summary>Built-in content. Always loaded first, and the lowest priority.</summary>
	public static ModInfo Core { get; } = new("eop", int.MinValue);
}
