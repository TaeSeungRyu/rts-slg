using Godot;

namespace SanguoSLG.Game;

public sealed partial class ScenarioStartMenu
{
    // Place a Godot-compatible intro.ogv here later. Without it the menu opens immediately.
    private void ShowOptionalIntro(Control menuCenter)
    {
        if (OS.GetCmdlineUserArgs().Contains("--startmenuqa")
            || OS.GetCmdlineUserArgs().Contains("--menulaunchqa")
            || OS.GetCmdlineUserArgs().Contains("--menuloadqa")) return;
        const string path = "res://assets/video/intro.ogv";
        if (!ResourceLoader.Exists(path)) return;
        var video = new VideoStreamPlayer { Stream = GD.Load<VideoStream>(path), Expand = true };
        video.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        video.MouseFilter = MouseFilterEnum.Stop;
        AddChild(video);
        menuCenter.Visible = false;
        var skip = new Button { Text = "건너뛰기" };
        skip.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);
        skip.Position = new Vector2(-145, 24);
        skip.Size = new Vector2(120, 40);
        video.AddChild(skip);
        void Finish()
        {
            if (!GodotObject.IsInstanceValid(video)) return;
            video.Stop();
            video.QueueFree();
            menuCenter.Visible = true;
        }
        video.Finished += Finish;
        skip.Pressed += Finish;
        video.Play();
    }
}
