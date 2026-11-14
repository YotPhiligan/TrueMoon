#version 450
layout(location=0) in vec3 position;
layout(push_constant) uniform Scene { mat4 transform; vec4 color; } scene;
void main() { gl_Position = scene.transform * vec4(position,1); }
