using Godot;
using SanguoSLG.Core.Domain;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
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
        var tree = _modalLayer?.FindChild("ComposeOfficerTable", true, false) as Tree;
        var passed = modal is not null && GetViewport().GetVisibleRect().Encloses(modal.GetGlobalRect())
            && tree?.Columns == 5 && tree.Size.Y >= 480f
            && modal.LeftScroll.VerticalScrollMode == ScrollContainer.ScrollMode.Auto
            && modal.FooterContent.FindChildren("*", "Button", true, false).Count > 0;
        GD.Print($"[transport-compose-ui-qa] passed={passed} size={modal?.Size} officer_table_height={tree?.Size.Y}");
        CloseModal();
        GetTree().Quit(passed ? 0 : 1);
    }
}
