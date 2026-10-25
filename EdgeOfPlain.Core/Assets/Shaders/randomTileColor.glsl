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
flat out float v_tileHash;

float hash(vec2 p)
{
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

void main()
{
    v_texCoord = texCoord;

    // The provoking vertex is always the tile's top-left, so these flat
    // values are the tile origin and a stable per-tile hash.
    v_tileOrigin = texCoord;
    v_tileHash = hash(floor(position / 32.0));

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
flat in float v_tileHash;

out vec4 fragColor;

void main()
{
    // Pick one of the 32 gradient columns for the whole tile.
    float column = floor(v_tileHash * 32.0);

    // fwidth is per screen pixel; convert back to the tile's UV size.
    float tileWidth = fwidth(v_texCoord.x) * 32.0 * u_zoom;
    float tileHeight = fwidth(v_texCoord.y) * 32.0 * u_zoom;

    vec2 tileSize = vec2(tileWidth, tileHeight);
    vec2 halfTexel = 0.5 / vec2(textureSize(u_texture, 0));
    vec2 local = vec2(
        (column + 0.5) / 32.0 * tileWidth,
        0.5 * tileHeight);
    local = clamp(local, halfTexel, max(tileSize - halfTexel, halfTexel));

    fragColor = texture(u_texture, v_tileOrigin + local);
}
