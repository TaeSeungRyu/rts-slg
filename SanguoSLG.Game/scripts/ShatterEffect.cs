using System.Collections.Generic;
using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 깨지는 듯한 효과(design-effect.md #11). <b>유닛 전용</b>. 오버레이가 아니라 대상의 실제
/// 메시를 <see cref="MeshFracture"/>로 잘게(삼각형 수에 따라 6~20조각) 쪼갠 뒤, 원본을 숨기고
/// 조각이 바깥+위로 튕겨나가 중력에 떨어지며 회전한다. 조각은 원본 재질을 그대로 승계한다.
/// 실사용 1회성, 검수용은 주기마다 원본 복원+조각 리셋.
/// </summary>
public partial class ShatterEffect : Node3D
{
    public float S = 1f;
    public Node3D Target = null!;

    private const float Period = 2.6f;
    private const float ExplodeStart = 0.10f;
    private const float ExplodeEnd = 0.72f;   // 이후 원본 복원 + 조각 숨김(쉼)

    private List<MeshInstance3D> _originals = new();
    private List<MeshFracture.Fragment> _fragments = new();
    private float _t;

    public override void _Ready()
    {
        if (Target != null)
        {
            (_originals, _fragments) = MeshFracture.Build(this, Target, 0); // 0=자동(잘게)
        }
    }

    public override void _Process(double delta)
    {
        if (_fragments.Count == 0)
        {
            return;
        }

        _t += (float)delta;
        var cycle = Mathf.PosMod(_t / Period, 1f);

        var rest = cycle >= ExplodeEnd;
        foreach (var o in _originals)
        {
            o.Visible = rest;
        }
        foreach (var f in _fragments)
        {
            f.Node.Visible = !rest;
        }
        if (rest)
        {
            return;
        }

        // ExplodeStart 전엔 조각이 조립된 채(통짜). 이후 폭발.
        var tt = Mathf.Clamp((cycle - ExplodeStart) / (ExplodeEnd - ExplodeStart), 0f, 1f);
        foreach (var f in _fragments)
        {
            var dir = (f.Dir + Vector3.Up * 0.4f).Normalized();
            var dist = (0.20f + (f.Seed % 4) * 0.10f) * tt * S;
            var drop = tt * tt * 0.7f * S;
            f.Node.Position = f.Rest + dir * dist - Vector3.Up * drop;
            var axis = new Vector3((f.Seed % 3) - 1, (f.Seed % 5) - 2, (f.Seed % 2) == 0 ? 1 : -1).Normalized();
            f.Node.Rotation = axis * ((4f + f.Seed % 5) * tt);
        }
    }
}
