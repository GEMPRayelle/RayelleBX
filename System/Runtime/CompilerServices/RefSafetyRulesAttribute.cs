// RefSafetyRulesAttribute.cs — C# 11+ 언어 기능(ref struct, scoped 키워드 등)을 사용할 때
// Roslyn 컴파일러가 내부적으로 이 어트리뷰트를 어셈블리에 삽입하려 한다.
//
// 문제: 이 어트리뷰트는 .NET 7+ 런타임에만 기본 내장되어 있다.
// 이 프로젝트는 Unity/BepInEx 환경이라 .NET Framework 4.8을 타겟으로 하므로
// 컴파일러가 참조할 정의가 없어 빌드 오류가 발생한다.
//
// 해결책: 동일한 네임스페이스·클래스명으로 직접 선언해 컴파일러에게 "이미 있다"고 알려준다.
// [Embedded] 어트리뷰트나 using Microsoft.CodeAnalysis 없이 최소한으로 유지할 것.

using System.Runtime.InteropServices;

namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Module, AllowMultiple = false, Inherited = false)]
internal sealed class RefSafetyRulesAttribute : Attribute
{
    public readonly int Version;

    public RefSafetyRulesAttribute([In] int obj0) => this.Version = obj0;
}
