using Godot;
using SanguoSLG.Core.Data;

namespace SanguoSLG.Game;

public partial class CampaignMapScene
{
    private void OpenSaveSlotWorkspace(bool loading)
    {
        var viewport = GetViewport().GetVisibleRect().Size;
        var width = Mathf.Min(960f, viewport.X - 40f);
        var height = Mathf.Min(720f, viewport.Y - 40f);
        var box = SystemView(loading ? "게임 불러오기" : "게임 저장", width,
            out var outerScroll, out var panel, out var titleRow);
        ApplyUnitMenuFrame(panel);
        panel.CustomMinimumSize = new Vector2(width, height);
        outerScroll.Name = loading ? "LoadSlotOuterScroll" : "SaveSlotOuterScroll";
        outerScroll.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
        outerScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        box.AddChild(MakeLabel(loading ? "불러올 기록을 선택하세요." : "저장할 기록을 선택하세요.",
            14, Parchment));

        var listScroll = new ScrollContainer { Name = loading ? "LoadSlotListScroll" : "SaveSlotListScroll",
            CustomMinimumSize = new Vector2(0, height - 110f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        box.AddChild(listScroll);
        var listInset = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        listInset.AddThemeConstantOverride("margin_right", 10);
        listScroll.AddChild(listInset);
        var grid = new GridContainer { Name = loading ? "LoadSlotGrid" : "SaveSlotGrid",
            Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 10);
        listInset.AddChild(grid);

        foreach (var info in SaveSlots.InspectAll())
        {
            var slot = info.Slot;
            var button = MakeButton("");
            button.Name = $"{(loading ? "Load" : "Save")}Slot{slot:00}";
            button.SetMeta(loading ? "load_slot_index" : "save_slot_index", slot);
            button.CustomMinimumSize = new Vector2(0, 124);
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            button.Disabled = loading && (!info.Exists || info.Corrupt);
            button.AddThemeStyleboxOverride("normal", Frame(InkSoft, Gold, 1, 8, 9));
            button.AddThemeStyleboxOverride("hover", Frame(new Color("512018"), GoldBright, 2, 8, 9));
            grid.AddChild(button);
            var content = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            content.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            content.AddThemeConstantOverride("margin_left", 12);
            content.AddThemeConstantOverride("margin_right", 12);
            content.AddThemeConstantOverride("margin_top", 9);
            content.AddThemeConstantOverride("margin_bottom", 9);
            button.AddChild(content);
            var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", 11);
            content.AddChild(row);
            var portrait = new TextureRect { Name = "SaveSlotRulerPortrait",
                CustomMinimumSize = new Vector2(76, 98),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = Control.MouseFilterEnum.Ignore };
            if (info.RulerId is { } rulerId)
            {
                var path = $"res://assets/portraits/{rulerId}.png";
                if (Godot.FileAccess.FileExists(path)) portrait.Texture = GD.Load<Texture2D>(path);
            }
            if (portrait.Texture is null && !string.IsNullOrWhiteSpace(info.RulerPortraitPath)
                && Godot.FileAccess.FileExists(info.RulerPortraitPath))
                portrait.Texture = GD.Load<Texture2D>(info.RulerPortraitPath);
            row.AddChild(portrait);
            var details = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore };
            details.AddThemeConstantOverride("separation", 3);
            row.AddChild(details);
            details.AddChild(MakeLabel($"기록 {slot:00} · {(info.Corrupt ? "손상됨" : info.Exists ? "저장됨" : "빈 슬롯")}", 15, GoldBright));
            if (!info.Exists || info.Corrupt)
            {
                details.AddChild(MakeLabel(info.Corrupt ? "저장 파일을 읽을 수 없습니다." : "새 게임 기록을 저장할 수 있습니다.",
                    12, Parchment));
            }
            else
            {
                details.AddChild(MakeLabel($"{info.FactionName ?? "세력 미상"} · 군주 {info.RulerName ?? "미상"}", 13, Parchment));
                details.AddChild(MakeLabel($"진행 {SlotStatus(info, "시기 미상")} · 거점 {info.OwnedSiteCount?.ToString() ?? "?"}",
                    12, Parchment));
                details.AddChild(MakeLabel(info.SavedAtUtc is { } saved
                    ? $"저장 {saved.ToLocalTime():yyyy-MM-dd HH:mm}" : "저장 시각 미상", 12, Gold));
            }
            if (loading)
            {
                button.TooltipText = info.Corrupt ? "손상된 저장 파일은 불러올 수 없습니다."
                    : !info.Exists ? "빈 슬롯입니다." : "";
                button.Pressed += () => ShowConfirm("게임 불러오기 확인",
                    $"현재 진행을 종료하고 슬롯 {slot:00}의 게임을 불러오시겠습니까?", () => LoadGame(slot));
            }
            else
            {
                var action = info.Exists ? "저장 데이터를 덮어쓰시겠습니까?" : "저장하시겠습니까?";
                button.Pressed += () => ShowConfirm("게임 저장 확인",
                    $"슬롯 {slot:00}에 현재 진행 상황을 {action}", () => SaveGame(slot));
            }
        }
        CenterAndDrag(panel, titleRow, width, height, box);
    }
}
