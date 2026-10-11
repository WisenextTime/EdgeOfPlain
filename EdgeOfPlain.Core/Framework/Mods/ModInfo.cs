using System;
using Tomlyn.Serialization;

namespace EdgeOfPlain.Core.Framework.Mods;

[method: TomlConstructor]
public readonly record struct ModInfo(
	[property: TomlRequired] string Id,
	[property: TomlRequired] Version Version,
	string Author = "Unknown",
	string Description = "",
	ModDependenceInfo[]? Dependence = null);

public readonly record struct ModDependenceInfo(
	[property: TomlRequired] string Id,
	[property: TomlRequired] Version Version);
