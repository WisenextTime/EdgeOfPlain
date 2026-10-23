#version 330 core

//VERT

layout(location = 0) in vec2 position;
layout(location = 1) in vec4 color;
layout(location = 8) in vec2 texCoord;

uniform vec2 u_cameraPosition;
uniform vec2 u_cameraSize;
uniform float u_cameraRotation;

out vec2 v_texCoord;
flat out vec2 v_tileOrigin;

void main()
{
    v_texCoord = texCoord;
    v_tileOrigin = texCoord;

    vec2 local = position - u_cameraPosition;
    float c = cos(-u_cameraRotation);
    float s = sin(-u_cameraRotation);
    local = vec2(c * local.x - s * local.y, s * local.x + c * local.y);

    vec2 ndc = local / (u_cameraSize * 0.5);
    gl_Position = vec4(ndc.x, -ndc.y, 0.0, 1.0);
}

//FRAG

uniform sampler2D u_texture;
uniform float u_zoom;

in vec2 v_texCoord;
flat in vec2 v_tileOrigin;

out vec4 fragColor;

void main()
{
    vec2 tileSize = vec2(fwidth(v_texCoord.x), fwidth(v_texCoord.y)) * 32.0 * u_zoom;
    vec2 halfTexel = 0.5 / vec2(textureSize(u_texture, 0));
    vec2 local = clamp(v_texCoord - v_tileOrigin, halfTexel,
        max(tileSize - halfTexel, halfTexel));
    fragColor = texture(u_texture, v_tileOrigin + local);
}