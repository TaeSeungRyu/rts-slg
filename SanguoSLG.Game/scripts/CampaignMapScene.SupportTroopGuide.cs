namespace SanguoSLG.Game;

using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

public sealed partial class CampaignMapScene
{
    // 보급·수송은 고정 병종 템플릿이 아니라 여러 병종을 합쳐 편성한다.
    // 따라서 병종 데이터에 가짜 능력치를 추가하지 않고 설명 목록에만 표시한다.
    private void AppendSupportTroopGuideRows(Tree table, TreeItem root)
    {
        foreach (var (code, name, role, range) in new[]
            { ("supply", "보급부대", "보급", "편성별"), ("transport", "수송부대", "수송", "0") })
        {
            var row = table.CreateItem(root);
            row.SetMetadata(0, code);
            row.SetText(0, name);
            row.SetIcon(0, RoundedOptionTexture(UnitCard(code, TroopClass.Supply), 36));
            row.SetIconMaxWidth(0, 36);
            row.SetText(1, role);
            row.SetText(2, code == "transport" ? "공격 불가" : "편성별");
            row.SetText(3, code == "transport" ? "공격 불가" : "편성별");
            row.SetText(4, "편성별");
            row.SetText(5, "2");
            row.SetText(6, range);
            row.SetText(7, code == "transport" ? "0" : "1");
            row.SetText(8, code == "transport" ? "0" : "1");
        }
    }

    private void OpenSupportTroopGuideDetail(string code)
    {
        var supply = code == "supply";
        const float width = 720f;
        const float height = 390f;
        var box = SystemView(supply ? "보급부대" : "수송부대", width,
            out var scroll, out var panel, out var titleRow);
        panel.Name = "TroopGuideDetailModal";
        ApplyUnitMenuFrame(panel);
        var back = titleRow.GetChild<Button>(0);
        back.ButtonDown -= OpenSystemPalette;
        back.ButtonDown += OpenTroopGuide;

        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 16);
        box.AddChild(top);
        top.AddChild(new TextureRect
        {
            Texture = UnitCard(code, TroopClass.Supply),
            CustomMinimumSize = new Vector2(188, 132),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        });
        var summary = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        top.AddChild(summary);
        summary.AddChild(MakeLabel(supply ? "역할  아군 보급" : "역할  도시 간 자원·병력 수송", 18, Gold));
        summary.AddChild(MakeLabel("이동  2칸/일  ·  공격·방어는 편성 병종에 따라 결정", 14, Parchment));
        summary.AddChild(MakeLabel(supply ? $"편성 병력  최대 {_cb.SupplyMaxTroops:N0}명"
            : $"편성 병력  최대 {DeployService.TransportMaxTroops:N0}명", 14, Parchment));
        box.AddChild(GoldRule());
        box.AddChild(MakeLabel("부대 특징", 18, Gold));
        foreach (var line in supply
            ? new[]
            {
                "여러 병종의 병력을 합쳐 편성하며 장수 한 명을 지정합니다.",
                "아군 부대에 병력·군량·금을 보충하고 보급 범위의 군량 소비를 줄입니다.",
                "공격·방어는 편성 가능한 병종의 최저 기본값을 적용합니다.",
                "휴대 군량·금과 보급 효율은 실제 편성 및 장수 적성에 따라 달라집니다.",
            }
            : new[]
            {
                "여러 병종의 병력과 군량·금을 다른 아군 성으로 수송합니다.",
                "수송 장수 한 명을 지정하며, 목적지와 출전 지연을 설정할 수 있습니다.",
                "공격과 반격을 하지 않으며 부대·건물·성 사거리는 0입니다.",
                "방어와 휴대 군량 상한은 편성 병종과 병력에 따라 달라집니다.",
            })
            box.AddChild(MakeLabel($"• {line}", 15, Parchment));

        scroll.CustomMinimumSize = new Vector2(width, height);
        CenterAndDrag(panel, titleRow, width, height, box);
    }
}
