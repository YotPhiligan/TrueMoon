#version 450
layout(location=0) in vec2 uv;
layout(set=0, binding=0) uniform sampler2D baseColor;
layout(push_constant) uniform Scene { mat4 transform; vec4 color; } scene;
layout(location=0) out vec4 outputColor;

void main() {
    vec3 linear = clamp(texture(baseColor, uv).rgb * scene.color.rgb, 0, 1);
    vec3 encoded = mix(1.055*pow(linear,vec3(1.0/2.4))-0.055,12.92*linear,lessThanEqual(linear,vec3(0.0031308)));
    outputColor = vec4(encoded, 1);
}
