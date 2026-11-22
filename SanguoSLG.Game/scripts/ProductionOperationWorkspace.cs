using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 일반연구 작업 공간의 좌측 선택 목록·우측 상세/요약·고정 실행부 구조를
/// 생산 작전 용어로 재사용한다. 생산 계산과 선택 상태는 CampaignMapScene이 소유한다.
/// </summary>
public sealed partial class ProductionOperationWorkspace : DoctrineResearchWorkspace
{
    public ProductionOperationWorkspace()
    {
        Name = "ProductionOperationWorkspace";
        Configure("투입 병종", true, "수행 장수", "생산 시작");
        DoctrineList.Name = "ProductionTargetList";
        DetailHost.Name = "ProductionTargetDetail";
        FundingPanel.Name = "ProductionTroopPanel";
        FundingSummary.Name = "ProductionTroopSummary";
        FundingChangeButton.Name = "ProductionTroopChange";
        OfficerSummary.Name = "ProductionOfficerSummary";
        OfficerChangeButton.Name = "ProductionOfficerChange";
        TotalCost.Name = "ProductionResultSummary";
        StartButton.Name = "ProductionStart";
    }
}
