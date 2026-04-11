namespace RayelleBX;

/// <summary>
/// 플러그인 메타데이터 상수 모음.
/// BepInEx는 이 값들로 플러그인을 식별하고 관리한다.
///
/// - PLUGIN_GUID : 플러그인 고유 식별자. 다른 플러그인과 절대 겹치면 안 된다.
///                 관례적으로 "com.제작자.플러그인명" 형식을 사용한다.
/// - PLUGIN_NAME : BepInEx 로그 및 설정 파일에 표시되는 사람이 읽기 쉬운 이름.
/// - PLUGIN_VERSION : 시맨틱 버전(Major.Minor.Patch). 모드 매니저가 업데이트 체크에 사용한다.
/// </summary>
public static class MyPluginInfo
{
    public const string PLUGIN_GUID = "com.example.RayelleBX";
    public const string PLUGIN_NAME = "RayelleBX";
    public const string PLUGIN_VERSION = "1.0.0";
}
