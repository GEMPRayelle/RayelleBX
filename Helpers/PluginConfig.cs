// PluginConfig.cs — setting.cfg 파일을 읽어 플러그인 설정을 관리한다.
//
// [cfg 파일 위치]
// BepInEx\plugins\RayelleBX\setting.cfg
// 빌드 시 DeployPlugin Target이 DLL과 함께 복사한다.
// 파일이 없으면 경고 로그를 출력하고 기본값(모두 true)을 사용한다.
//
// [포맷]
// - '#'로 시작하는 줄은 주석
// - key = value 형식
// - 불리언 값은 true / false (대소문자 무관)
// - WishCostume 은 다중행 특수 처리:
//     WishCostume =          ← 이 줄 이후부터 코스튬 항목 수집 시작
//     301,세헤라자드_푸른 마녀  ← "{숫자},{이름}" 패턴이면 wish 목록에 추가
//     ...
//   다른 key = value 줄을 만나거나 파일 끝이면 수집 종료
//
// [게임 재시작 없이 반영하려면]
// Load()를 다시 호출하면 된다. 현재는 Plugin.Awake()에서 한 번만 호출한다.

using BepInEx;
using System;
using System.Collections.Generic;
using System.IO;

namespace RayelleBX.Helpers;

public static class PluginConfig
{
    private static readonly string ConfigPath =
        Path.Combine(Paths.PluginPath, "RayelleBX", "setting.cfg");

    // --- 기능 On/Off (기본값: 모두 활성화) ---
    public static bool QuickMenuMacro      { get; private set; } = true;
    public static bool CharRecoveryMacro   { get; private set; } = true;
    public static bool CharCostumeLogging  { get; private set; } = true;
    public static bool InfiniteGachaMacro  { get; private set; } = true;

    // --- 무한뽑기 매크로 설정 ---
    /// <summary>각 버튼 클릭 후 대기 시간 (초)</summary>
    public static float GachaStepDelay         { get; private set; } = 0.2f;
    /// <summary>결과 화면이 뜬 뒤 대기 시간 (초) — 애니메이션 완료 대기</summary>
    public static float GachaWaitResultTimeout { get; private set; } = 1.0f;
    /// <summary>10연차 결과에서 최소 UR 개수</summary>
    public static int   GachaMinUR             { get; private set; } = 2;
    /// <summary>결과 UR 중 WishCostume 목록에 있어야 하는 최소 개수</summary>
    public static int   GachaEssentialUR       { get; private set; } = 1;
    /// <summary>설정 파일에서 읽은 위시 코스튬 ID 집합</summary>
    public static HashSet<int> GachaWishCostumes { get; private set; } = new HashSet<int>();

    /// <summary>
    /// setting.cfg를 읽어 설정값을 갱신한다.
    /// 파일이 없거나 파싱 실패 시 기본값을 유지한다.
    /// </summary>
    public static void Load()
    {
        if (!File.Exists(ConfigPath))
        {
            Plugin.Log.LogWarning($"[PluginConfig] setting.cfg 없음, 기본값 사용: {ConfigPath}");
            return;
        }

        try
        {
            GachaWishCostumes = new HashSet<int>();
            bool readingWishCostumes = false;

            foreach (string line in File.ReadAllLines(ConfigPath))
            {
                string trimmed = line.Trim();

                // 빈 줄: WishCostume 수집 중이어도 건너뜀 (항목 사이 공백 허용)
                if (string.IsNullOrEmpty(trimmed)) continue;

                // 주석 줄
                if (trimmed.StartsWith("#")) continue;

                // "숫자,이름" 패턴 → WishCostume 항목 (수집 모드일 때만 처리)
                if (readingWishCostumes && char.IsDigit(trimmed[0]) && trimmed.IndexOf(',') > 0)
                {
                    string[] parts = trimmed.Split(new char[] { ',' }, 2);
                    if (int.TryParse(parts[0].Trim(), out int costumeId))
                        GachaWishCostumes.Add(costumeId);
                    continue;
                }

                // key = value 줄 — 새 키가 나오면 WishCostume 수집 종료
                int eqIndex = trimmed.IndexOf('=');
                if (eqIndex < 0) continue;

                readingWishCostumes = false; // 새 key 시작 → 코스튬 수집 중단

                string key   = trimmed.Substring(0, eqIndex).Trim();
                string value = trimmed.Substring(eqIndex + 1).Trim();

                switch (key)
                {
                    case "QuickMenuMacro":
                        QuickMenuMacro = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "CharRecoveryMacro":
                        CharRecoveryMacro = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "CharCostumeLogging":
                        CharCostumeLogging = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "InfiniteGachaMacro":
                        InfiniteGachaMacro = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "StepDelay":
                        if (float.TryParse(value, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out float sd))
                            GachaStepDelay = sd;
                        break;
                    case "WaitResultTimeout":
                        if (float.TryParse(value, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out float wrt))
                            GachaWaitResultTimeout = wrt;
                        break;
                    case "MinUR":
                        if (int.TryParse(value, out int minUr))
                            GachaMinUR = minUr;
                        break;
                    case "EssentialUR":
                        if (int.TryParse(value, out int essUr))
                            GachaEssentialUR = essUr;
                        break;
                    case "WishCostume":
                        // 값 자체가 비어 있으면 다음 줄부터 항목 수집 시작
                        readingWishCostumes = true;
                        if (!string.IsNullOrEmpty(value) && char.IsDigit(value[0]))
                        {
                            string[] parts = value.Split(new char[] { ',' }, 2);
                            if (int.TryParse(parts[0].Trim(), out int id))
                                GachaWishCostumes.Add(id);
                        }
                        break;
                }
            }

            Plugin.Log.LogInfo(
                $"[PluginConfig] 로드 완료 — " +
                $"QuickMenuMacro={QuickMenuMacro}, " +
                $"CharRecoveryMacro={CharRecoveryMacro}, CharCostumeLogging={CharCostumeLogging}, " +
                $"InfiniteGachaMacro={InfiniteGachaMacro} " +
                $"(StepDelay={GachaStepDelay}, WaitResultTimeout={GachaWaitResultTimeout}, " +
                $"MinUR={GachaMinUR}, EssentialUR={GachaEssentialUR}, " +
                $"WishCostumes={GachaWishCostumes.Count}개)");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[PluginConfig] 파싱 실패, 기본값 사용: {e.Message}");
        }
    }
}
