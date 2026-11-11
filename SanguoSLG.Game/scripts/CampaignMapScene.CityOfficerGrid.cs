using Godot;
using SanguoSLG.Core.Domain;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private GridContainer BuildCityOfficerGrid(City city)
    {
        var grid = new GridContainer { Name = "CityOfficerGrid", Columns = 2,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 10);
        foreach (var (role, officerId) in new[] { ("태수", city.Governor), ("군사", city.Strategist) })
        {
            var frame = new PanelContainer { Name = "CityOfficerCard",
                CustomMinimumSize = new Vector2(0, 64),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var style = new StyleBoxFlat { BgColor = new Color(InkSoft),
                BorderColor = new Color(Gold, .42f) };
            style.SetBorderWidthAll(1);
            style.SetCornerRadiusAll(8);
            style.SetContentMarginAll(6);
            frame.AddThemeStyleboxOverride("panel", style);
            frame.SetMeta("city_officer_card", true);
            var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Begin };
            row.AddThemeConstantOverride("separation", 8);
            frame.AddChild(row);
            var portraitCell = new CenterContainer { CustomMinimumSize = new Vector2(52, 52),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            var picture = new TextureRect { Name = "CityOfficerPortrait",
                Texture = officerId is { } id ? CircularPortraitFor(id) : null,
                CustomMinimumSize = new Vector2(52, 52),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize };
            portraitCell.AddChild(picture);
            row.AddChild(portraitCell);
            var stack = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            };
            stack.AddThemeConstantOverride("separation", 3);
            row.AddChild(stack);
            var name = officerId is { } nameId
                ? _state.Generals.FirstOrDefault(general => general.Id == nameId)?.Name ?? "없음"
                : "없음";
            foreach (var (caption, size, color) in new[]
                { (role, 11, GoldBright), (name, 14, Parchment) })
            {
                var label = MakeLabel(caption, size, color);
                label.CustomMinimumSize = new Vector2(0, caption == role ? 18 : 22);
                label.HorizontalAlignment = HorizontalAlignment.Left;
                label.VerticalAlignment = VerticalAlignment.Center;
                label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                stack.AddChild(label);
            }
            grid.AddChild(frame);
        }
        return grid;
    }

    private void RunCityOfficerGridQa()
    {
        var city = _state.Cities.First(candidate => candidate.Owner == Player);
        var generals = _state.Generals.Take(2).ToArray();
        if (generals.Length < 2) { GetTree().Quit(1); return; }
        _state = _state with { Cities = _state.Cities.Select(candidate => candidate.Id == city.Id
            ? candidate with { Governor = generals[0].Id, Strategist = generals[1].Id }
            : candidate).ToList() };
        SelectCity(city.Id);
        var grid = _infoRows.FindChild("CityOfficerGrid", true, false) as GridContainer;
        var divider = _infoRows.FindChild("CityResourceDivider", true, false) as MarginContainer;
        var scroll = _infoCard.FindChild("CityInfoScroll", true, false) as ScrollContainer;
        var actions = _infoCard.FindChild("CityInfoActions", true, false) as VBoxContainer;
        var labels = grid?.FindChildren("*", "Label", true, false).OfType<Label>()
            .Select(label => label.Text).ToList() ?? [];
        var passed = grid?.Columns == 2 && grid.GetChildCount() == 2
            && grid.GetChildren().OfType<PanelContainer>().Count(panel =>
                panel.GetMeta("city_officer_card").AsBool()) == 2
            && labels.Count == 4 && labels[0] == "태수" && labels[2] == "군사"
            && grid.FindChildren("CityOfficerPortrait", "TextureRect", true, false).OfType<TextureRect>()
                .Count(portrait => portrait.Texture is not null) == 2
            && grid.FindChildren("*", "HBoxContainer", true, false).Count >= 2
            && divider?.GetThemeConstant("margin_top") is >= 4 and <= 6
            && scroll?.VerticalScrollMode == ScrollContainer.ScrollMode.Auto
            && actions?.FindChildren("*", "Button", true, false).Count > 0;
        GD.Print($"[city-officer-grid-qa] passed={passed} labels={string.Join(',', labels)} cards={grid?.GetChildCount()} divider={divider?.GetThemeConstant("margin_top")} portraits={grid?.FindChildren("CityOfficerPortrait", "TextureRect", true, false).Count}");
        GetTree().Quit(passed ? 0 : 1);
    }

    private async void RunRightUiLayoutQa()
    {
        var argument = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--maptestrightuiqa=", System.StringComparison.Ordinal));
        var resolution = argument?.EndsWith("1920x1080", System.StringComparison.Ordinal) == true
            ? new Vector2I(1920, 1080) : new Vector2I(1600, 900);
        GetWindow().Size = resolution;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var city = _state.Cities.First(candidate => candidate.Owner == Player && !candidate.IsPort);
        SelectCity(city.Id);
        ToggleGroup(0);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var viewport = GetViewport().GetVisibleRect().Size;
        var info = _infoCard.GetGlobalRect();
        var command = _cmdMenu.GetGlobalRect();
        var submenu = _cmdSubMenu.GetGlobalRect();
        var gap = info.Position.X - command.End.X;
        var passed = _infoCard.Visible && _cmdMenu.Visible && _cmdSubMenu.Visible
            && Mathf.Abs(info.End.X - (viewport.X - 12f)) <= 1f
            && Mathf.Abs(gap - 10f) <= 1f
            && command.End.X < info.Position.X
            && submenu.End.X < command.Position.X
            && info.Position.Y >= 8f && info.End.Y <= viewport.Y - 8f
            && command.Position.Y >= 8f && command.End.Y <= viewport.Y - 8f;
        var outputPath = System.IO.Path.Combine(
            new System.IO.DirectoryInfo(ProjectSettings.GlobalizePath("res://")).Parent?.FullName
                ?? ProjectSettings.GlobalizePath("res://"),
            $"shot_right_ui_{resolution.X}x{resolution.Y}.png");
        var saveResult = GetViewport().GetTexture().GetImage().SavePng(outputPath);
        passed &= saveResult == Error.Ok;
        var port = _state.Cities.FirstOrDefault(candidate => candidate.Owner == Player && candidate.IsPort);
        var portOk = true;
        if (port is not null)
        {
            SelectCity(port.Id);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            portOk = _infoCard.Visible && _infoRows.FindChildren("*", "Label", true, false)
                .OfType<Label>().Any(label => label.Text.StartsWith("항구 · 시야", System.StringComparison.Ordinal));
            passed &= portOk;
        }
        GD.Print($"[right-ui-qa] passed={passed} resolution={resolution} info={info} command={command} submenu={submenu} gap={gap} port={portOk} shot={outputPath}");
        GetTree().Quit(passed ? 0 : 1);
    }
}
