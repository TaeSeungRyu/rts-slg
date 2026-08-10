using System.Collections.Generic;
using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 찢어지는 듯한 효과(design-effect.md #10). <b>유닛 전용</b>. 대상의 실제 메시를 모델 전체
/// 기준 4조각으로 쪼갠 뒤(<see cref="MeshFracture"/>), 원본을 숨기고 조각이 바깥으로 천천히
/// 갈라져 벌어졌다 떨어진다 — 잘게 터지는 <see cref="ShatterEffect"/>와 달리 큰 덩어리가
/// 뜯겨나가는 느낌. 실사용 1회성, 검수용은 주기마다 원본 복원.
/// </summary>
public partial class TearEffect : Node3D
{
    public float S = 1f;
    public Node3D Target = null!;

    private const int Pieces = 4;
    private const float Period = 2.8f;
    private const float TearStart = 0.12f;
    private const float TearEnd = 0.76f;   // 이후 원본 복원 + 조각 숨김(쉼)

    private List<MeshInstance3D> _originals = new();
    private List<MeshFracture.Fragment> _fragments = new();
    private float _t;

    public override void _Ready()
    {
        if (Target != null)
        {
            (_originals, _fragments) = MeshFracture.Build(this, Target, Pieces);
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

        var rest = cycle >= TearEnd;
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

        // TearStart 전엔 조각이 조립된 채(통짜). 이후 큰 덩어리가 천천히 갈라진다.
        var tt = Mathf.Clamp((cycle - TearStart) / (TearEnd - TearStart), 0f, 1f);
        foreach (var f in _fragments)
        {
            var dist = (0.10f + (f.Seed % 4) * 0.04f) * tt * S; // 잘게 안 터지고 천천히 벌어짐
            var drop = tt * tt * 0.35f * S;
            f.Node.Position = f.Rest + f.Dir * dist - Vector3.Up * drop;
            var axis = new Vector3((f.Seed % 3) - 1, 1f, (f.Seed % 2) == 0 ? 1 : -1).Normalized();
            f.Node.Rotation = axis * (1.4f * tt);
        }
    }
}
