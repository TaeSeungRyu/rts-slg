using System.Collections.Generic;
using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 병력 규모에 따른 편대 배치(doc/spec-unit.md 계획 1). 1기짜리 모델을 N개 복제해 세운다.
/// 좌표는 타일 중심 기준 로컬, 정면은 +Z. 타일 변심거리 0.5 안에 들어가도록 잡았다.
/// </summary>
public static class TroopFormation
{
    /// <summary>검수·편성에 쓰는 규모 단계.</summary>
    public static readonly int[] Sizes = { 1, 3, 5, 7, 9 };

    /// <summary>편대원 한 명의 자리 — 타일 로컬 오프셋과 미세 yaw(도).</summary>
    public readonly record struct Slot(Vector3 Offset, float YawDegrees);

    private static readonly Slot[] One =
    {
        new(new Vector3(0f, 0f, 0f), 0f),
    };

    // 쐐기
    private static readonly Slot[] Three =
    {
        new(new Vector3(0f, 0f, 0.13f), 0f),
        new(new Vector3(-0.15f, 0f, -0.05f), 6f),
        new(new Vector3(0.15f, 0f, -0.05f), -5f),
    };

    // 쐐기 + 후열 2
    private static readonly Slot[] Five =
    {
        new(new Vector3(0f, 0f, 0.18f), 0f),
        new(new Vector3(-0.16f, 0f, 0.03f), 5f),
        new(new Vector3(0.16f, 0f, 0.03f), -4f),
        new(new Vector3(-0.08f, 0f, -0.15f), -6f),
        new(new Vector3(0.08f, 0f, -0.15f), 7f),
    };

    // 쐐기 2열
    private static readonly Slot[] Seven =
    {
        new(new Vector3(0f, 0f, 0.22f), 0f),
        new(new Vector3(-0.16f, 0f, 0.07f), 5f),
        new(new Vector3(0.16f, 0f, 0.07f), -6f),
        new(new Vector3(-0.32f, 0f, -0.08f), 8f),
        new(new Vector3(0f, 0f, -0.08f), -3f),
        new(new Vector3(0.32f, 0f, -0.08f), -7f),
        new(new Vector3(0f, 0f, -0.23f), 4f),
    };

    // 마름모(1·2·3·2·1). 폭 ±0.32는 육각 변심거리 0.5 안, 앞뒤 ±0.27은 꼭짓점 0.5774 안이다.
    private static readonly Slot[] Nine =
    {
        new(new Vector3(0f, 0f, 0.27f), 0f),
        new(new Vector3(-0.17f, 0f, 0.13f), 5f),
        new(new Vector3(0.17f, 0f, 0.13f), -6f),
        new(new Vector3(-0.32f, 0f, -0.01f), 8f),
        new(new Vector3(0f, 0f, -0.01f), -3f),
        new(new Vector3(0.32f, 0f, -0.01f), -7f),
        new(new Vector3(-0.17f, 0f, -0.15f), -5f),
        new(new Vector3(0.17f, 0f, -0.15f), 6f),
        new(new Vector3(0f, 0f, -0.28f), 3f),
    };

    /// <summary>규모에 해당하는 자리 목록. 정의되지 않은 규모는 가장 가까운 아래 단계를 쓴다.</summary>
    public static Slot[] Slots(int count) => count switch
    {
        >= 9 => Nine,
        >= 7 => Seven,
        >= 5 => Five,
        >= 3 => Three,
        _ => One,
    };

    /// <summary>1기짜리 모델을 규모만큼 복제해 root 아래에 세운다.</summary>
    public static void Build(Node3D root, PackedScene model, int count)
    {
        var index = 0;
        foreach (var slot in Slots(count))
        {
            var member = model.Instantiate<Node3D>();
            member.Position = slot.Offset;
            member.RotationDegrees = new Vector3(0f, slot.YawDegrees, 0f);
            ApplyVariant(member, index++);
            root.AddChild(member);
        }
    }

    // 개체 변이: 모델에 variant_0..N 그룹이 있으면 편대원 순번마다 한 벌만 남긴다.
    // 짐승 무늬처럼 같은 병종이라도 개체마다 생김새가 달라야 하는 모델이 쓰는 규약이다.
    private static void ApplyVariant(Node3D member, int index)
    {
        var variants = new List<Node3D>();
        for (var v = 0; member.FindChild($"variant_{v}", true, false) is Node3D group; v++)
        {
            variants.Add(group);
        }

        for (var v = 0; v < variants.Count; v++)
        {
            variants[v].Visible = v == index % variants.Count;
        }
    }
}
