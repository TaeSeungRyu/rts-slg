namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 밸런스 상수 묶음. 코드에 매직 넘버로 박지 않고 data/balance.json에서 주입한다.
/// 스켈레톤 단계의 값은 파이프 검증용 임시값이며, 실제 밸런스는 이후 설계에서 정한다.
/// </summary>
public sealed record BalanceConfig(int MonthlyTaxPerCity);
