#version 450
layout(push_constant) uniform Scene { mat4 transform; vec4 color; } scene;
layout(location=0) flat in float lighting;
layout(location=0) out vec4 outputColor;
void main() { outputColor = vec4(scene.color.rgb * lighting,1); }
