shader_type canvas_item;

// 화면 가장자리를 어둡게 해 초점·깊이를 준다.
uniform float strength : hint_range(0.0, 1.0) = 0.5;
uniform float inner : hint_range(0.0, 1.0) = 0.35;
uniform float outer : hint_range(0.0, 1.5) = 0.9;

void fragment() {
    float d = distance(UV, vec2(0.5));
    float v = smoothstep(inner, outer, d);
    COLOR = vec4(0.0, 0.0, 0.0, v * strength);
}
