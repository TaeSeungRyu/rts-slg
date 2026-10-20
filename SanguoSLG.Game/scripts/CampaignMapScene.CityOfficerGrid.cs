using Godot;
using SanguoSLG.Core.Domain;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private GridContainer BuildCityOfficerGrid(City city)
    {
        var grid = new GridContainer { Name = "CityOfficerGrid", Columns = 2,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 0);
        grid.AddThemeConstantOverride("v_separation", 0);
        PanelContainer Cell(Control child)
        {
            var frame = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var style = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0),
                BorderColor = new Color(Gold, .6f) };
            style.SetBorderWidthAll(1);
            style.SetContentMarginAll(3);
            frame.AddThemeStyleboxOverride("panel", style);
            frame.SetMeta("city_officer_grid_cell", true);
            frame.AddChild(child);
            return frame;
        }
        foreach (var officerId in new[] { city.Governor, city.Strategist })
        {
            var portraitCell = new CenterContainer { CustomMinimumSize = new Vector2(125, 72),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var picture = new TextureRect { Name = "CityOfficerPortrait",
                Texture = officerId is { } id ? CircularPortraitFor(id) : null,
                CustomMinimumSize = new Vector2(64, 64),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize };
            portraitCell.AddChild(picture);
            grid.AddChild(Cell(portraitCell));
        }
        foreach (var (role, officerId) in new[] { ("태수", city.Governor), ("군사", city.Strategist) })
        {
            var name = officerId is { } id
                ? _state.Generals.FirstOrDefault(general => general.Id == id)?.Name ?? "없음"
                : "없음";
            var label = MakeLabel($"{role}\n{name}", 13, Parchment);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            grid.AddChild(Cell(label));
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
        var labels = grid?.FindChildren("*", "Label", true, false).OfType<Label>()
            .Select(label => label.Text).ToList() ?? [];
        var passed = grid?.Columns == 2 && grid.GetChildCount() == 4
            && grid.GetChildren().OfType<PanelContainer>().Count(panel =>
                panel.GetMeta("city_officer_grid_cell").AsBool()) == 4
            && labels.Count == 2 && labels[0].StartsWith("태수\n") && labels[1].StartsWith("군사\n")
            && grid.FindChildren("CityOfficerPortrait", "TextureRect", true, false).OfType<TextureRect>()
                .Count(portrait => portrait.Texture is not null) == 2;
        GD.Print($"[city-officer-grid-qa] passed={passed} labels={string.Join(',', labels)}");
        GetTree().Quit(passed ? 0 : 1);
    }
}
