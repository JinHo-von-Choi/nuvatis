namespace NuVatis.Statement;

/**
 * SQL 구문의 타입을 정의하는 열거형.
 *
 * @author 최진호
 * @date   2026-02-24
 */
/// <summary>SQL 구문의 타입을 정의하는 열거형.</summary>
public enum StatementType {
    /// <summary>SELECT 쿼리. 데이터를 조회한다.</summary>
    Select,
    /// <summary>INSERT 구문. 새 행을 삽입한다.</summary>
    Insert,
    /// <summary>UPDATE 구문. 기존 행을 수정한다.</summary>
    Update,
    /// <summary>DELETE 구문. 기존 행을 삭제한다.</summary>
    Delete
}
