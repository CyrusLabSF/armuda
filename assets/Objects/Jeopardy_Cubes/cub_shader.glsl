#version 330 core
in vec3 a_pos;
in vec2 a_uv;
out vec2 v_uv;
uniform mat4 u_model;
uniform mat4 u_viewProj;
uniform float u_time;

void main() {
    float angle = u_time * 0.6;
    mat4 rotY = mat4(
        cos(angle), 0.0, sin(angle), 0.0,
        0.0, 1.0, 0.0, 0.0,
        -sin(angle), 0.0, cos(angle), 0.0,
        0.0, 0.0, 0.0, 1.0
    );
    gl_Position = u_viewProj * u_model * rotY * vec4(a_pos, 1.0);
    v_uv = a_uv;
}
