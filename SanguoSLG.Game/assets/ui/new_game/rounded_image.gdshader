shader_type canvas_item;

uniform vec2 rect_size = vec2(132.0, 74.0);
uniform float corner_radius = 6.0;

void fragment() {
    vec4 color = texture(TEXTURE, UV) * COLOR;
    vec2 point = UV * rect_size;
    vec2 half_size = rect_size * 0.5;
    vec2 distance_to_corner = abs(point - half_size) - (half_size - vec2(corner_radius));
    float signed_distance = length(max(distance_to_corner, vec2(0.0))) + min(max(distance_to_corner.x, distance_to_corner.y), 0.0) - corner_radius;
    color.a *= 1.0 - smoothstep(-0.5, 0.5, signed_distance);
    COLOR = color;
}
