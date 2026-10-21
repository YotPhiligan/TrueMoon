#version 450
layout(push_constant) uniform Scene { mat4 transform; vec4 color; } scene;
layout(location=0) flat out float lighting;
// Faces deliberately submitted front before back: depth testing must retain the front face.
const vec3 positions[8] = vec3[8](
    vec3(-.5,-.5,.5), vec3(.5,-.5,.5), vec3(.5,.5,.5), vec3(-.5,.5,.5),
    vec3(-.5,-.5,-.5),vec3(.5,-.5,-.5),vec3(.5,.5,-.5),vec3(-.5,.5,-.5));
const int indices[36] = int[36](0,1,2,2,3,0, 5,4,7,7,6,5, 4,0,3,3,7,4,
    1,5,6,6,2,1, 3,2,6,6,7,3, 4,5,1,1,0,4);
const float lights[6] = float[6](1.0,.3,.65,.8,.9,.45);
void main() {
    gl_Position = scene.transform * vec4(positions[indices[gl_VertexIndex]],1);
    lighting = lights[gl_VertexIndex / 6];
}
