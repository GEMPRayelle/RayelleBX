// CostumeConfig.cs — 코스튬 ID ↔ 이름 매핑을 CSV 파일로 저장·조회한다.
//
// [파일 위치]
// BepInEx\plugins\RayelleBX\Resources\CostumeMapping.csv
// 디렉터리가 없으면 EnsureFile()이 자동으로 생성한다.
//
// [CSV 구조]
// CostumeID,CostumeName      ← 헤더 (최초 생성 시 삽입)
// 1234,캐릭터명_코스튬명
// ...
//
// [사용 흐름]
// CharUIEnablePatch가 코스튬 탭을 열 때마다 AppendMapping()을 호출해 누적 저장한다.
// 나중에 다른 기능에서 LoadAllCostumes()로 저장된 ID를 읽어 활용할 수 있다.

using BepInEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RayelleBX.Config;

public static class CostumeConfig
{
    // Paths.PluginPath : BepInEx가 제공하는 plugins 폴더 절대 경로
    public static readonly string CsvFilePath = Path.Combine(
        Paths.PluginPath, "RayelleBX", "Resources", "CostumeMapping.csv");

    /// <summary>
    /// CSV 파일과 상위 디렉터리가 없으면 새로 만든다.
    /// AppendMapping/LoadAllCostumes 호출 전에 항상 실행된다.
    /// </summary>
    public static void EnsureFile()
    {
        try
        {
            string dir = Path.GetDirectoryName(CsvFilePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            if (!File.Exists(CsvFilePath))
                // 헤더 한 줄만 넣어 파일을 생성한다
                File.WriteAllText(CsvFilePath, "CostumeID,CostumeName\n", Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError("Costume CSV 초기화 실패: " + ex.Message);
        }
    }

    /// <summary>
    /// 코스튬 ID와 이름을 CSV에 기록한다 (upsert).
    /// - ID 없음 → 새 줄 추가 (true)
    /// - ID 있고 기존 이름이 빈 값 또는 "_?" 플레이스홀더이며 신규 이름이 실제 이름 → 해당 줄 업데이트 (true)
    /// - 그 외(이미 실제 이름 보유 등) → 변경 없음 (false)
    /// </summary>
    public static bool AppendMapping(object costumeId, string costumeName)
    {
        try
        {
            EnsureFile();

            if (!(costumeId is int id))
            {
                if (!int.TryParse(costumeId?.ToString(), out id)) return false;
            }

            string[] lines = File.ReadAllLines(CsvFilePath, Encoding.UTF8);
            int existingIndex = -1;
            string existingName = null;

            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]) || lines[i].StartsWith("CostumeID")) continue;
                string[] parts = lines[i].Split(new char[] { ',' }, 2);
                if (parts.Length >= 1 && int.TryParse(parts[0], out int rowId) && rowId == id)
                {
                    existingIndex = i;
                    existingName = parts.Length >= 2 ? parts[1] : "";
                    break;
                }
            }

            if (existingIndex == -1)
            {
                File.AppendAllText(CsvFilePath, $"{id},{costumeName}" + Environment.NewLine, Encoding.UTF8);
                return true;
            }

            bool existingIsPlaceholder = string.IsNullOrEmpty(existingName) || existingName.EndsWith("_?");
            bool newIsReal = !string.IsNullOrEmpty(costumeName) && !costumeName.EndsWith("_?");

            if (existingIsPlaceholder && newIsReal)
            {
                lines[existingIndex] = $"{id},{costumeName}";
                File.WriteAllLines(CsvFilePath, lines, Encoding.UTF8);
                Plugin.Log.LogInfo($"[CostumeConfig] 이름 업데이트: {id}  {existingName} → {costumeName}");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError("Costume CSV 저장 실패: " + ex.Message);
            return false;
        }
    }

    /// <summary>
    /// CSV에 저장된 모든 CostumeID를 HashSet으로 반환한다.
    /// int 파싱에 실패한 행은 조용히 무시한다.
    /// </summary>
    public static HashSet<int> LoadAllCostumes()
    {
        var set = new HashSet<int>();
        try
        {
            EnsureFile();
            foreach (string raw in File.ReadAllLines(CsvFilePath, Encoding.UTF8))
            {
                // 빈 줄과 헤더 행은 건너뜀
                if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("CostumeID"))
                    continue;
                // Split(new char[]{ ',' }, 2) — .NET Framework 4.8에서는 Split(char, int) 오버로드 없음
                string[] parts = raw.Split(new char[] { ',' }, 2);
                if (parts.Length >= 1 && int.TryParse(parts[0], out int id))
                    set.Add(id);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError("코스튬 로드 실패: " + ex.Message);
        }
        return set;
    }
}
