namespace NuVatis.Statement;

/**
 * MyBatis <selectKey> 설정 모델.
 * INSERT 전후에 키 생성 SQL을 실행하여 결과를 파라미터 객체에 설정한다.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public sealed class SelectKeyConfig {
    public required string         KeyProperty { get; init; }
    public required string         Sql         { get; init; }
    public required SelectKeyOrder Order       { get; init; }
    public string?                 ResultType  { get; init; }
}

/**
 * selectKey 실행 시점.
 */
public enum SelectKeyOrder {
    Before,
    After
}
