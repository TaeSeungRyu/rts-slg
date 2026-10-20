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

    private void OpenSkillGuide()
    {
        const float width = 900f;
        const float height = 620f;
        var box = SystemView("스킬설명", width, out var scroll, out var panel, out var titleRow);
        var entries = _activeSkills.OrderBy(skill => skill.Name, StringComparer.Ordinal)
            .Select(skill => ($"액티브 · {skill.Name}", SkillDescriptions.Active(skill)))
            .Concat(_passiveSkills.OrderBy(skill => skill.Name, StringComparer.Ordinal)
                .Select(skill => ($"전투 패시브 · {skill.Name}",
                    string.Join("\n\n", Enumerable.Range(1, 3)
                        .Select(level => SkillDescriptions.Passive(skill, level))))))
            .Concat(_adminSkills.OrderBy(skill => skill.Name, StringComparer.Ordinal)
                .Select(skill => ($"내정 패시브 · {skill.Name}",
                    string.Join("\n\n", Enumerable.Range(1, 3)
                        .Select(level => SkillDescriptions.Admin(skill, level))))))
            .ToList();
        var search = new LineEdit { Name = "SkillGuideSearch", PlaceholderText = "스킬 이름 검색",
            MaxLength = 30 };
        box.AddChild(search);
        var columns = new HBoxContainer { CustomMinimumSize = new Vector2(0, 480),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 12);
        box.AddChild(columns);
        var list = new ItemList { Name = "SkillGuideList", CustomMinimumSize = new Vector2(300, 480),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        list.AddThemeFontOverride("font", _font);
        columns.AddChild(list);
        var detailScroll = new ScrollContainer { Name = "SkillGuideDetailScroll",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        columns.AddChild(detailScroll);
        var detail = MakeLabel("", 15, Parchment);
        detail.Name = "SkillGuideDetail";
        detail.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        detail.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        detailScroll.AddChild(detail);
        var shown = entries;
        void Refill(string query)
        {
            shown = entries.Where(entry => entry.Item1.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
            list.Clear();
            foreach (var entry in shown) list.AddItem(entry.Item1);
            detail.Text = shown.Count == 0 ? "일치하는 스킬이 없습니다." : shown[0].Item2;
            if (shown.Count > 0) list.Select(0);
        }
        list.ItemSelected += index => detail.Text = shown[(int)index].Item2;
        search.TextChanged += Refill;
        Refill("");
        scroll.CustomMinimumSize = new Vector2(width, height);
        CenterAndDrag(panel, titleRow, width, height, box);
    }

    private void RunSkillGuideQa()
    {
        OpenSystemPalette();
        var trigger = _modalLayer?.FindChildren("*", "Button", true, false).OfType<Button>()
            .FirstOrDefault(button => button.Text == "스킬설명");
        trigger?.EmitSignal(Button.SignalName.Pressed);
        var list = _modalLayer?.FindChild("SkillGuideList", true, false) as ItemList;
        var search = _modalLayer?.FindChild("SkillGuideSearch", true, false) as LineEdit;
        var detail = _modalLayer?.FindChild("SkillGuideDetail", true, false) as Label;
        var initialCount = list?.ItemCount ?? 0;
        search?.EmitSignal(LineEdit.SignalName.TextChanged, "무쌍");
        var passed = trigger is not null && initialCount == _activeSkills.Count + _passiveSkills.Count + _adminSkills.Count
            && list?.ItemCount == 1 && detail?.Text.Contains("일반 공격 피해", StringComparison.Ordinal) == true;
        GD.Print($"[skill-guide-qa] passed={passed} entries={initialCount}");
        GetTree().Quit(passed ? 0 : 1);
    }
}
