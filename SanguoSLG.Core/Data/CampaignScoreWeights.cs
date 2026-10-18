namespace SanguoSLG.Core.Data;

using System.Text.Json;

public sealed record CampaignScoreWeights(int City, int ResearchLevel, int CapturedCity,
    int TroopsPerPoint, int FatalitiesPerPoint, int Stratagem = 100, int RuinStrengthDivisor = 100)
{
    public static CampaignScoreWeights LoadFromDirectory(string directory)
    {
        var path = Path.Combine(directory, "campaign-score.json");
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        var weights = JsonSerializer.Deserialize<CampaignScoreWeights>(File.ReadAllText(path), options)
            ?? throw new InvalidDataException("세력 기록 점수 설정을 읽을 수 없습니다.");
        if (weights.TroopsPerPoint <= 0 || weights.FatalitiesPerPoint <= 0 || weights.RuinStrengthDivisor <= 0)
            throw new InvalidDataException("세력 기록 점수의 병력 나눗수가 잘못되었습니다.");
        return weights;
    }
}
