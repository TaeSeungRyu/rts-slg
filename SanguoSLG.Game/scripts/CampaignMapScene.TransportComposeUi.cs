using Godot;
using SanguoSLG.Core.Domain;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private Control BuildTransportComposeHeroCard(GeneralId id)
    {
        var general = _state.Generals.First(item => item.Id == id);
        var card = new Control
        {
            Name = "TransportSelectedGeneralCard",
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
        var portrait = FixedIcon(OfficerPortrait(id), 74);
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
        text.AddChild(MakeLabel($"주장   {general.Name}", 17, GoldBright));
        text.AddChild(MakeLabel($"보급 적성 {GradeText(general.AptitudeFor(TroopClass.Supply))}", 13, Parchment));
        text.AddChild(MakeLabel($"무 {general.Might} · 지 {general.Intellect} · 정 {general.Politics}", 13, Parchment));
        row.AddChild(text);
        return card;
    }

    private async void RunTransportComposeUiQa()
    {
        var argument = OS.GetCmdlineUserArgs().FirstOrDefault(value => value.StartsWith("--maptesttransportcomposeuiqa=", StringComparison.Ordinal));
        if (argument is not null)
        {
            var dimensions = argument.Split('=', 2)[1].Split('x', 2);
            if (dimensions.Length == 2 && int.TryParse(dimensions[0], out var width)
                && int.TryParse(dimensions[1], out var height)) GetWindow().Size = new Vector2I(width, height);
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var city = _state.Cities.First(candidate => candidate.Owner == Player
            && _state.Cities.Any(destination => destination.Owner == candidate.Owner && destination.Id != candidate.Id)
            && _state.Garrisons.Any(garrison => garrison.City == candidate.Id && garrison.Troops > 0 && !garrison.Trainee)
            && _state.GeneralsAt(candidate.Id).Any(id => !OfficerUnavailable(id)));
        OpenTransportCompose(city.Id);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var modal = _modalLayer?.FindChild("TransportComposeModal", true, false) as TransportComposeModal;
        var card = _modalLayer?.FindChild("TransportSelectedGeneralCard", true, false) as Control;
        var tree = _modalLayer?.FindChild("ComposeOfficerTable", true, false) as Tree;
        var passed = modal is not null && GetViewport().GetVisibleRect().Encloses(modal.GetGlobalRect())
            && card is not null && tree?.Columns == 5
            && modal.LeftScroll.VerticalScrollMode == ScrollContainer.ScrollMode.Auto
            && modal.FooterContent.FindChildren("*", "Button", true, false).Count > 0;
        GD.Print($"[transport-compose-ui-qa] passed={passed} size={modal?.Size} card={card is not null}");
        CloseModal();
        GetTree().Quit(passed ? 0 : 1);
    }
}
