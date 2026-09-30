namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>항구 담당 업무 효율. 일반 도시는 100%, 소형 항구 50%, 중형 항구 75%다.</summary>
public static class PortAdministration
{
    public static int EfficiencyPercent(City city) => city.Port switch
    {
        PortSize.Small => 50,
        PortSize.Medium => 75,
        _ => 100,
    };

    public static int Scale(City city, int amount) => amount * EfficiencyPercent(city) / 100;
}
