#version 450

layout(location = 0) in vec2 Position;
layout(location = 1) in vec2 TexCoord;
layout(location = 2) in vec4 Color;

layout(set = 0, binding = 0) uniform ProjectionBuffer {
    mat4 Projection;
};

layout(location = 0) out vec4 fsin_Color;
layout(location = 1) out vec2 fsin_TexCoord;

void main() {
    fsin_Color    = Color;
    fsin_TexCoord = TexCoord;
    gl_Position   = Projection * vec4(Position, 0.0, 1.0);
}