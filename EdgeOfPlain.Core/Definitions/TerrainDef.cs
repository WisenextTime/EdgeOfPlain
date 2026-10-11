using System;
using System.Collections.Generic;
using Tomlyn.Serialization;

namespace EdgeOfPlain.Core.Definitions;

[ContentTypeName("terrain")]
[method: TomlConstructor]
public class TerrainDef(string id) : ContentDef(id)
{
	[Flags]
	public enum PassageMask
	{
		None = 0,
		Ground = 1 << 0,
		Water = 1 << 1,
		Air = 1 << 2,
		Cliff = 1 << 3,
	}
	public PassageMask Passage { get; set; } = PassageMask.None;
	public int MoveCost { get; set; } = 1;
	public List<string> Tags { get; set; } = [];
	public bool Allows(PassageMask required) => (Passage & required) != PassageMask.None;
}