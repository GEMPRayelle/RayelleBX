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
//
// [게임 재시작 없이 반영하려면]
// Load()를 다시 호출하면 된다. 현재는 Plugin.Awake()에서 한 번만 호출한다.

using BepInEx;
using System;
using System.IO;

namespace RayelleBX.Helpers;

public static class PluginConfig
{
    private static readonly string ConfigPath =
        Path.Combine(Paths.PluginPath, "RayelleBX", "setting.cfg");

    // 기본값은 모두 활성화
    public static bool OverwhelmIndicator  { get; private set; } = true;
    public static bool QuickMenuMacro      { get; private set; } = true;
    public static bool CharRecoveryMacro   { get; private set; } = true;
    public static bool CharCostumeLogging  { get; private set; } = true;

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
            foreach (string line in File.ReadAllLines(ConfigPath))
            {
                string trimmed = line.Trim();

                // 빈 줄 및 주석 건너뜀
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                    continue;

                int eqIndex = trimmed.IndexOf('=');
                if (eqIndex < 0) continue;

                string key   = trimmed.Substring(0, eqIndex).Trim();
                string value = trimmed.Substring(eqIndex + 1).Trim();

                switch (key)
                {
                    case "OverwhelmIndicator":
                        OverwhelmIndicator = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "QuickMenuMacro":
                        QuickMenuMacro = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "CharRecoveryMacro":
                        CharRecoveryMacro = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "CharCostumeLogging":
                        CharCostumeLogging = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                        break;
                }
            }

            Plugin.Log.LogInfo($"[PluginConfig] 로드 완료 — OverwhelmIndicator={OverwhelmIndicator}, QuickMenuMacro={QuickMenuMacro}, CharRecoveryMacro={CharRecoveryMacro}, CharCostumeLogging={CharCostumeLogging}");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[PluginConfig] 파싱 실패, 기본값 사용: {e.Message}");
        }
    }
}
