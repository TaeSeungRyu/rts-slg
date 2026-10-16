using Godot;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private void RunCombatProvisionQa()
    {
        try
        {
            var city = _state.Cities.First(candidate => candidate.Owner == Player && !candidate.IsPort);
            _depModalCity = city.Id;
            OpenDeployCompose(-1);
            var troop = _state.Garrisons.First(force => force.City == city.Id && force.Troops >= 10000);
            _depTroop = troop.TroopCode;
            _depAmountSpin!.MaxValue = 10000;
            _depAmountSpin.Value = 10000;
            UpdateCombatProvisionCapacity(followMaximum: true);
            var originalMaximum = _depProvSlider!.MaxValue;
            _depAmountSpin.Value = 5000;
            var reducedMaximum = _depProvSlider.MaxValue;
            var valueChangePassed = reducedMaximum > 0 && reducedMaximum < originalMaximum
                && _depProvSlider.Value == reducedMaximum
                && ProvisionsToCarry() == (int)reducedMaximum;
            var input = _depAmountSpin.GetLineEdit();
            input.Text = "2500";
            input.EmitSignal(LineEdit.SignalName.TextChanged, input.Text);
            var typedMaximum = _depProvSlider.MaxValue;
            var passed = valueChangePassed && typedMaximum > 0 && typedMaximum < reducedMaximum
                && _depAmount == 2500 && _depProvSlider.Value == typedMaximum
                && ProvisionsToCarry() == (int)typedMaximum;
            GD.Print($"[maptestcombatprovisionqa] passed={passed} before={originalMaximum} after={reducedMaximum} typed={typedMaximum} selected={_depProvSlider.Value}");
            GetTree().Quit(passed ? 0 : 1);
        }
        catch (System.Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
