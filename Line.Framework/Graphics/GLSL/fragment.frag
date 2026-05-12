#version 450

layout(set = 0, binding = 1) uniform texture2D SurfaceTexture;
layout(set = 0, binding = 2) uniform sampler SurfaceSampler;

layout(location = 0) in vec4 fsin_Color;
layout(location = 1) in vec2 fsin_TexCoord;

layout(location = 0) out vec4 OutputColor;

void main() {
    vec4 texColor = texture(sampler2D(SurfaceTexture, SurfaceSampler), fsin_TexCoord);
    OutputColor   = texColor * fsin_Color;   // 纹理色与顶点色相乘，支持 Alpha 调制
}