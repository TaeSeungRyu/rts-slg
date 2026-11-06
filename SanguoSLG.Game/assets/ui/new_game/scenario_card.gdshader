shader_type canvas_item;

uniform vec2 rect_size = vec2(360.0, 108.0);
uniform vec4 upper_color : source_color = vec4(0.12, 0.09, 0.08, 1.0);
uniform vec4 lower_color : source_color = vec4(0.10, 0.08, 0.07, 1.0);
uniform vec4 border_color : source_color = vec4(0.36, 0.29, 0.20, 1.0);
uniform float border_width = 1.0;
uniform float corner_radius = 6.0;

float rounded_distance(vec2 point, vec2 size, float radius) {
    vec2 half_size = size * 0.5;
    vec2 corner = abs(point - half_size) - (half_size - vec2(radius));
    return length(max(corner, vec2(0.0))) + min(max(corner.x, corner.y), 0.0) - radius;
}

void fragment() {
    vec2 point = UV * rect_size;
    float outer = rounded_distance(point, rect_size, corner_radius);
    float inner = rounded_distance(point - vec2(border_width), rect_size - vec2(border_width * 2.0), max(0.0, corner_radius - border_width));
    vec4 fill = mix(upper_color, lower_color, UV.y);
    float border = 1.0 - smoothstep(-0.5, 0.5, inner);
    COLOR = mix(border_color, fill, border);
    COLOR.a *= 1.0 - smoothstep(-0.5, 0.5, outer);
}
