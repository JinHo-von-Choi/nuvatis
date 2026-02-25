namespace NuVatis.Attributes;

/**
 * NuVatis Source Generator가 프록시 구현체를 생성할 대상 인터페이스를 표시한다.
 *
 * 이 어트리뷰트가 없는 인터페이스는 Generator의 스캔 대상에서 제외된다.
 * AutoMapper 등 외부 라이브러리의 인터페이스와의 충돌을 방지하기 위한
 * 명시적 opt-in 메커니즘이다.
 *
 * @author 최진호
 * @date   2026-02-25
 */
[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class NuVatisMapperAttribute : Attribute {
}
