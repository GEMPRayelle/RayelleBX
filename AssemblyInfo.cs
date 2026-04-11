// AssemblyInfo.cs — 어셈블리(DLL) 수준 메타데이터를 선언하는 파일.
// 유니티 프로젝트의 Package Manager 정보와 비슷한 역할이다.
//
// SkipVerification = true
//   BepInEx 플러그인은 게임 DLL의 internal/private 멤버에 접근해야 하는 경우가 많다.
//   .NET CLR의 IL 검증을 건너뛰어 이를 허용한다. 런타임 패치 환경에서는 일반적인 설정이다.

using System.Reflection;
using System.Security.Permissions;

[assembly: AssemblyCompany("RayelleBX")]
[assembly: AssemblyConfiguration("Debug")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0")]
[assembly: AssemblyProduct("RayelleBX")]
[assembly: AssemblyTitle("RayelleBX")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
