using System.Collections.Generic;
using Tomlyn.Serialization;

namespace EdgeOfPlain.Core.Definitions;

[ContentTypeName("tileRenderer")]
public class TileRendererDef(string id) : ContentDef(id)
{
	[TomlRequired]
	public string ShaderPath { get; set; } = "";
	public Dictionary<string, ShaderUniform> Uniforms = [];
}

public struct ShaderUniform
{
	public enum UniformType
	{
		Float, Int, Bool,
		Vec2, Vec3, Vec4,
		Mat3, Mat4,
		Sampler2D,
	}

	[TomlRequired] public UniformType Type;
	public float[]? Value;
	public string? Texture;
}