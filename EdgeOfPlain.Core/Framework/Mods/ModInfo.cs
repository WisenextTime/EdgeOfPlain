using Tomlyn.Serialization;

namespace EdgeOfPlain.Core.Framework.Mods;

[method: TomlConstructor]
public readonly record struct ModInfo(
	[property: TomlRequired] string Id,
	string Version,
	string Author = "Unknown",
	string Description = "");
