using Godot;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private void OpenTroopGuide()
    {
        const float width = 850f;
        const float height = 650f;
        var box = SystemView("부대설명", width, out var scroll, out var panel, out var titleRow);
        box.AddChild(MakeLabel("병종별 기본 능력 · 실제 전투력은 병력 수, 장수 적성, 연구와 지형에 따라 달라집니다.", 13, Parchment));
        foreach (var troop in _troops.OrderBy(t => t.Class).ThenBy(t => t.Name, StringComparer.Ordinal))
        {
            var row = new PanelContainer();
            row.AddThemeStyleboxOverride("panel", Frame(InkSoft, Gold, 1, 6, 7));
            var text = MakeLabel($"{troop.Name}  ·  {ClassName(troop.Class)}\n"
                + $"부대 공격 {troop.AtkUnit}  |  건물 공격 {troop.AtkBuilding}  |  방어 {troop.Df}"
                + $"  |  이동 {troop.MovementPerDay}  |  사거리 부대/건물/성 {troop.RangeUnit}/{troop.RangeBuilding}/{troop.RangeCastle}",
                13, Parchment);
            row.AddChild(text);
            box.AddChild(row);
        }
        scroll.CustomMinimumSize = new Vector2(width, height);
        CenterAndDrag(panel, titleRow, width, height, box);
    }

    private void RunTroopGuideQa()
    {
        OpenSystemPalette();
        var trigger = _modalLayer?.FindChildren("*", "Button", true, false).OfType<Button>()
            .FirstOrDefault(button => button.Text == "부대설명");
        trigger?.EmitSignal(Button.SignalName.Pressed);
        var labels = _modalLayer?.FindChildren("*", "Label", true, false).OfType<Label>()
            .Select(label => label.Text).ToList() ?? [];
        var passed = trigger is not null && labels.Any(text => text.Contains("투석기"))
            && labels.Any(text => text.Contains("공성탑"))
            && _modalLayer?.FindChild("SystemViewBack", true, false) is Button;
        GD.Print($"[troop-guide-qa] passed={passed} entries={_troops.Count}");
        GetTree().Quit(passed ? 0 : 1);
    }
}
