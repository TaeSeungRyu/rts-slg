namespace SanguoSLG.Core.Tests;

/// <summary>테스트 공용 헬퍼. 실제 data 디렉토리 위치를 찾는다.</summary>
internal static class TestData
{
    // 테스트 바이너리 위치에서 위로 올라가며 리포지토리의 data 디렉토리를 찾는다.
    public static string DataDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "data");
            if (File.Exists(Path.Combine(candidate, "factions.json")))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("data 디렉토리를 찾지 못했습니다.");
    }
}
