using Godot;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private void RefreshArmyGroupHeroCards()
    {
        if (_deployComposeHeroCards is null || !GodotObject.IsInstanceValid(_deployComposeHeroCards)) return;
        foreach (var child in _deployComposeHeroCards.GetChildren())
        {
            _deployComposeHeroCards.RemoveChild(child);
            child.QueueFree();
        }
        _deployComposeHeroCards.AddChild(BuildArmyGroupHeroCard("선봉", _depVan));
        _deployComposeHeroCards.AddChild(BuildArmyGroupHeroCard("부관", _depAdj));
    }

    private Control BuildArmyGroupHeroCard(string role, SanguoSLG.Core.Domain.GeneralId? id)
    {
        var card = new Control
        {
            CustomMinimumSize = new Vector2(285, 108),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipContents = true,
        };
        var background = new TextureRect
        {
            Texture = GD.Load<Texture2D>(DeployCommandAssetRoot + "hero_card.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        card.AddChild(background);
        var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        card.AddChild(margin);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 10);
        margin.AddChild(row);
        if (id is not { } generalId)
        {
            var empty = MakeLabel($"{role}\n선택되지 않음", 14, Parchment);
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.VerticalAlignment = VerticalAlignment.Center;
            empty.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(empty);
            return card;
        }
        var general = _state.Generals.First(item => item.Id == generalId);
        var portrait = FixedIcon(OfficerPortrait(generalId), 74);
        portrait.CustomMinimumSize = new Vector2(74, 74);
        portrait.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        portrait.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(portrait);
        var text = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        text.AddChild(MakeLabel($"{role}   {general.Name}", 17, GoldBright));
        text.AddChild(MakeLabel($"집단군 적성 {GradeText(ArmyGroupAptitude(general))}", 13, Parchment));
        text.AddChild(MakeLabel($"무 {general.Might} · 지 {general.Intellect} · 정 {general.Politics}", 13, Parchment));
        row.AddChild(text);
        return card;
    }

    private async void RunArmyGroupComposeUiQa()
    {
        var argument = OS.GetCmdlineUserArgs().FirstOrDefault(value => value.StartsWith("--maptestarmygroupcomposeuiqa=", StringComparison.Ordinal));
        if (argument is not null)
        {
            var dimensions = argument.Split('=', 2)[1].Split('x', 2);
            if (dimensions.Length == 2 && int.TryParse(dimensions[0], out var width)
                && int.TryParse(dimensions[1], out var height)) GetWindow().Size = new Vector2I(width, height);
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var city = _state.Cities.First(candidate => candidate.Owner == Player);
        _depModalCity = city.Id;
        OpenArmyGroupCompose(-1);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var modal = _modalLayer?.FindChild("ArmyGroupComposeModal", true, false) as ArmyGroupComposeModal;
        var grid = _modalLayer?.FindChild("ArmyGroupTroopGrid", true, false) as GridContainer;
        var cards = _modalLayer?.FindChild("ArmyGroupHeroCards", true, false) as HBoxContainer;
        var table = _modalLayer?.FindChild("ComposeOfficerTable", true, false) as Tree;
        var passed = modal is not null && GetViewport().GetVisibleRect().Encloses(modal.GetGlobalRect())
            && grid?.Columns == 2 && cards?.GetChildCount() == 2 && table?.Columns == 8
            && modal.LeftScroll.VerticalScrollMode == ScrollContainer.ScrollMode.Auto;
        GD.Print($"[army-group-compose-ui-qa] passed={passed} size={modal?.Size} cards={cards?.GetChildCount()}");
        CloseModal();
        GetTree().Quit(passed ? 0 : 1);
    }
}
