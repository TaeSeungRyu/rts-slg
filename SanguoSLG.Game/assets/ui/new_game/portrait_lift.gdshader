shader_type canvas_item;

uniform vec2 rect_size = vec2(142.0, 202.0);
uniform float corner_radius = 8.0;
uniform float shadow_lift = 0.13;
uniform vec3 warm_light = vec3(1.0, 0.78, 0.48);

void fragment() {
    vec4 color = texture(TEXTURE, UV) * COLOR;
    float luminance = dot(color.rgb, vec3(0.2126, 0.7152, 0.0722));
    float lift = shadow_lift * (1.0 - smoothstep(0.18, 0.82, luminance));
    color.rgb = min(vec3(1.0), color.rgb + warm_light * lift);
    vec2 point = UV * rect_size;
    vec2 half_size = rect_size * 0.5;
    vec2 corner = abs(point - half_size) - (half_size - vec2(corner_radius));
    float distance = length(max(corner, vec2(0.0)))
        + min(max(corner.x, corner.y), 0.0) - corner_radius;
    color.a *= 1.0 - smoothstep(-0.5, 0.5, distance);
    COLOR = color;
}
