#version 450
layout(location=0) in vec3 position;
layout(location=1) in vec2 uv0;
layout(location=0) out vec2 uv;
layout(push_constant) uniform Scene { mat4 transform; vec4 color; } scene;

void main() {
    uv = uv0;
    gl_Position = scene.transform * vec4(position, 1);
}
