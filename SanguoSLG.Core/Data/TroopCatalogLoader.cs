namespace SanguoSLG.Core.Data;

using SanguoSLG.Core.Domain;

/// <summary>기본 병종과 기반 병종을 상속하는 특수병과를 생산/편성용 단일 목록으로 합친다.</summary>
public sealed class TroopCatalogLoader
{
    public IReadOnlyList<TroopTemplate> LoadFromDirectory(string dataDirectory)
    {
        var result = new TroopTypeLoader().LoadFromDirectory(dataDirectory).ToList();
        var byCode = result.ToDictionary(t => t.Code, StringComparer.Ordinal);
        foreach (var special in new SpecialUnitLoader().LoadFromDirectory(dataDirectory))
        {
            if (!byCode.TryGetValue(special.BaseCode, out var basis)) continue;
            result.Add(basis with
            {
                Code = special.Code,
                Name = special.Name,
                Df = special.DfOverride ?? basis.Df,
                AtkBuilding = special.BuildingAtkOverride ?? basis.AtkBuilding,
            });
        }
        return result;
    }
}
