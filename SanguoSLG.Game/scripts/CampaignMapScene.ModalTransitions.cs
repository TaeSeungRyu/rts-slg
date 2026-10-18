namespace SanguoSLG.Game;

using Godot;

public sealed partial class CampaignMapScene
{
    private const float ModalFadeSeconds = 0.1f;

    private void TickModalTransitions()
    {
        FadeInIfNew(_modalLayer);
        FadeInIfNew(_confirmLayer);
    }

    private void FadeInIfNew(CanvasLayer? layer)
    {
        if (layer is null || !GodotObject.IsInstanceValid(layer) || !layer.IsInsideTree()
            || layer.HasMeta("modal_fade_started")) return;
        layer.SetMeta("modal_fade_started", true);
        foreach (var child in layer.GetChildren())
        {
            if (child is not Control control) continue;
            control.Modulate = new Color(1, 1, 1, 0);
            CreateTween().TweenProperty(control, "modulate:a", 1f, ModalFadeSeconds);
        }
    }

    private void FadeOutModal(CanvasLayer? layer)
    {
        if (layer is null || !GodotObject.IsInstanceValid(layer) || layer.IsQueuedForDeletion()) return;
        if (!layer.IsInsideTree()) { layer.QueueFree(); return; }
        if (layer.HasMeta("modal_fade_exiting")) return;
        layer.SetMeta("modal_fade_exiting", true);
        var tween = CreateTween().SetParallel(true);
        foreach (var child in layer.GetChildren())
        {
            if (child is not Control control) continue;
            IgnoreModalInput(control);
            tween.TweenProperty(control, "modulate:a", 0f, ModalFadeSeconds);
        }
        tween.Chain().TweenCallback(Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(layer)) layer.QueueFree();
        }));
    }

    private static void IgnoreModalInput(Control control)
    {
        control.MouseFilter = Control.MouseFilterEnum.Ignore;
        foreach (var child in control.GetChildren())
            if (child is Control nested) IgnoreModalInput(nested);
    }

    private async void RunModalTransitionQa()
    {
        OpenFactionScoreboard();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var layer = _modalLayer;
        var opened = layer is not null && layer.HasMeta("modal_fade_started");
        FadeOutModal(layer);
        _modalLayer = null;
        await ToSignal(GetTree().CreateTimer(0.15), SceneTreeTimer.SignalName.Timeout);
        var passed = opened && !GodotObject.IsInstanceValid(layer);
        GD.Print($"[maptestmodaltransitionqa] opened={opened} released={!GodotObject.IsInstanceValid(layer)} passed={passed}");
        GetTree().Quit(passed ? 0 : 1);
    }
}
