#version 450
layout(location = 0) in vec2 uv;
layout(location = 0) out vec4 color;
layout(push_constant) uniform Scene { vec4 left; vec4 right; } scene;
void main() { color = uv.x < 0.5 ? scene.left : scene.right; }
