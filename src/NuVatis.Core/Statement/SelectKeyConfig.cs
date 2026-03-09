namespace NuVatis.Statement;

/**
 * MyBatis <selectKey> 설정 모델.
 * INSERT 전후에 키 생성 SQL을 실행하여 결과를 파라미터 객체에 설정한다.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public sealed class SelectKeyConfig {
    /// <summary>생성된 키 값을 설정할 파라미터 객체의 프로퍼티 이름.</summary>
    public required string         KeyProperty { get; init; }
    /// <summary>키를 생성하는 SQL 문. 단일 스칼라 값을 반환해야 한다.</summary>
    public required string         Sql         { get; init; }
    /// <summary>키 생성 SQL을 INSERT 전(Before) 또는 후(After)에 실행할지 지정한다.</summary>
    public required SelectKeyOrder Order       { get; init; }
    /// <summary>키 값의 .NET 타입 이름. null이면 반환된 값 타입을 그대로 사용한다.</summary>
    public string?                 ResultType  { get; init; }
}

/**
 * selectKey 실행 시점.
 */
public enum SelectKeyOrder {
    /// <summary>INSERT 실행 전에 키 생성 SQL을 실행한다.</summary>
    Before,
    /// <summary>INSERT 실행 후에 키 생성 SQL을 실행한다.</summary>
    After
}
