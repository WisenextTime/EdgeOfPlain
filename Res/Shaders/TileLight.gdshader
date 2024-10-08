shader_type canvas_item;
render_mode blend_mix;

uniform sampler2D LightTexture;
uniform vec4  LightColor : source_color; 

void fragment()
{
	if (texture(LightTexture,UV).a!=0.0)
	COLOR += vec4(LightColor.x * (sin(TIME)*10.0 +12.0)
	,LightColor.y * (sin(TIME)*10.0 +12.0),
	LightColor.z * (sin(TIME)*10.0 +12.0),
	texture(LightTexture,UV).a);
	COLOR.a = 1.0;
}