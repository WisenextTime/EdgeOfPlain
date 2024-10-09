shader_type spatial;
uniform vec4 Color:source_color = vec4(1.0,1.0,1.0,1.0);

void fragment()
{
	ALBEDO = Color.rgb;
	ALPHA = Color.a;
}
