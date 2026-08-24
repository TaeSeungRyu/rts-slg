namespace SanguoSLG.Core.Domain;

/// <summary>시장에서 금으로 사는 자원(design-administration "생산 자원과 시장").</summary>
public enum MarketResource
{
    /// <summary>광석 — 모든 병력 생산에 필요.</summary>
    Ore,

    /// <summary>말 — 기병 생산.</summary>
    Horses,

    /// <summary>코끼리 — 상병 생산.</summary>
    Elephants,

    /// <summary>군량 — 옵셔널 품목(약탈·보급 차단 등 비상 시 매입).</summary>
    Grain,
}
