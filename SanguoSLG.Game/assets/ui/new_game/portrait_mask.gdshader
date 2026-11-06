shader_type canvas_item;

uniform vec2 rect_size = vec2(142.0, 202.0);
uniform float corner_radius = 8.0;

void fragment() {
    vec2 point = UV * rect_size;
    vec2 half_size = rect_size * 0.5;
    vec2 corner = abs(point - half_size) - (half_size - vec2(corner_radius));
    float distance = length(max(corner, vec2(0.0)))
        + min(max(corner.x, corner.y), 0.0) - corner_radius;
    COLOR.a *= 1.0 - smoothstep(-0.5, 0.5, distance);
}
