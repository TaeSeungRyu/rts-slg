namespace SanguoSLG.Core.Domain;

/// <summary>두 세력 간 동맹 상태. 작은 id를 A, 큰 id를 B로 정규화해 중복을 막는다.</summary>
public sealed record FactionAlliance(FactionId A, FactionId B, int StartDay, int? EndDay = null)
{
    public static FactionAlliance Create(FactionId left, FactionId right, int startDay, int? endDay = null)
    {
        if (left == right)
        {
            throw new System.ArgumentException("같은 세력끼리는 동맹을 맺을 수 없다.");
        }

        return left.Value < right.Value
            ? new FactionAlliance(left, right, startDay, endDay)
            : new FactionAlliance(right, left, startDay, endDay);
    }

    public bool Contains(FactionId faction) => A == faction || B == faction;
    public bool Matches(FactionId left, FactionId right) => Contains(left) && Contains(right) && left != right;
    public bool ActiveOn(int day) => EndDay is null || day <= EndDay.Value;
}
