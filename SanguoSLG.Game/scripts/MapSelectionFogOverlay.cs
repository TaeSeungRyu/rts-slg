using Godot;

namespace SanguoSLG.Game;

public sealed partial class MapSelectionFogOverlay : CanvasLayer
{
    private const int MaxOpenings = 64;
    private readonly ShaderMaterial _material;

    public MapSelectionFogOverlay()
    {
        Layer = 17;
        var shader = new Shader
        {
            Code = """
                shader_type canvas_item;
                uniform vec3 openings[64];
                uniform int opening_count = 0;
                void fragment() {
                    float mask = 1.0;
                    for (int i = 0; i < opening_count; i++) {
                        if (distance(FRAGCOORD.xy, openings[i].xy) < openings[i].z) {
                            mask = 0.0;
                        }
                    }
                    COLOR = vec4(0.08, 0.12, 0.16, 0.72 * mask);
                }
                """,
        };
        _material = new ShaderMaterial { Shader = shader };
        var shade = new ColorRect { Color = Colors.White, MouseFilter = Control.MouseFilterEnum.Ignore,
            Material = _material };
        shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(shade);
    }

    public void SetOpenings(IReadOnlyList<Vector3> openings)
    {
        var values = new Vector3[MaxOpenings];
        var count = Mathf.Min(MaxOpenings, openings.Count);
        for (var index = 0; index < count; index++) values[index] = openings[index];
        _material.SetShaderParameter("openings", values);
        _material.SetShaderParameter("opening_count", count);
        SetMeta("opening_count", count);
    }
}
