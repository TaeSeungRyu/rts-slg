using Godot;
using SanguoSLG.Core.Domain;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private GridContainer BuildCityOfficerGrid(City city)
    {
        var grid = new GridContainer { Name = "CityOfficerGrid", Columns = 2,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 8);
        foreach (var (role, officerId) in new[] { ("태수", city.Governor), ("군사", city.Strategist) })
        {
            var frame = new PanelContainer { Name = "CityOfficerCard",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var style = new StyleBoxFlat { BgColor = new Color(InkSoft),
                BorderColor = new Color(Gold, .42f) };
            style.SetBorderWidthAll(1);
            style.SetCornerRadiusAll(8);
            style.SetContentMarginAll(6);
            frame.AddThemeStyleboxOverride("panel", style);
            frame.SetMeta("city_officer_card", true);
            var stack = new VBoxContainer();
            stack.AddThemeConstantOverride("separation", 2);
            frame.AddChild(stack);
            var portraitCell = new CenterContainer { CustomMinimumSize = new Vector2(0, 66),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var picture = new TextureRect { Name = "CityOfficerPortrait",
                Texture = officerId is { } id ? CircularPortraitFor(id) : null,
                CustomMinimumSize = new Vector2(64, 64),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize };
            portraitCell.AddChild(picture);
            stack.AddChild(portraitCell);
            var name = officerId is { } nameId
                ? _state.Generals.FirstOrDefault(general => general.Id == nameId)?.Name ?? "없음"
                : "없음";
            foreach (var (caption, size, color) in new[]
                { (role, 11, GoldBright), (name, 14, Parchment) })
            {
                var label = MakeLabel(caption, size, color);
                label.HorizontalAlignment = HorizontalAlignment.Center;
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
        var labels = grid?.FindChildren("*", "Label", true, false).OfType<Label>()
            .Select(label => label.Text).ToList() ?? [];
        var passed = grid?.Columns == 2 && grid.GetChildCount() == 2
            && grid.GetChildren().OfType<PanelContainer>().Count(panel =>
                panel.GetMeta("city_officer_card").AsBool()) == 2
            && labels.Count == 4 && labels[0] == "태수" && labels[2] == "군사"
            && grid.FindChildren("CityOfficerPortrait", "TextureRect", true, false).OfType<TextureRect>()
                .Count(portrait => portrait.Texture is not null) == 2
            && divider?.GetThemeConstant("margin_top") >= 10;
        GD.Print($"[city-officer-grid-qa] passed={passed} labels={string.Join(',', labels)} cards={grid?.GetChildCount()} divider={divider?.GetThemeConstant("margin_top")} portraits={grid?.FindChildren("CityOfficerPortrait", "TextureRect", true, false).Count}");
        GetTree().Quit(passed ? 0 : 1);
    }
}
