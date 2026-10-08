namespace HelperManager.Modules.TableInfo.Models;

/// <summary>
/// 네 개의 JSON 파일(Table / Column / Index / Object)을 서로 이어주는 조인 키(OWNER + TABLE_NAME)를
/// 만드는 헬퍼입니다. 선택한 테이블의 컬럼/인덱스/오브젝트를 매번 전체 목록에서 훑지 않고
/// 미리 만들어둔 Lookup 에서 바로 꺼낼 수 있도록, 두 값을 하나의 문자열 키로 합쳐둔다.
/// (컬럼 목록은 스키마 전체를 내보내면 수십만 건이 될 수 있어, 선택할 때마다 전체 스캔하면 느리다)
/// </summary>
public static class TableKey
{
    /// <summary>
    /// OWNER 와 TABLE_NAME 을 하나의 비교용 키로 합친다. 오라클 객체명은 대소문자를 구분하지 않으므로
    /// 대문자로 맞추고, 소유자명에 포함될 수 없는 문자('\u0001')로 구분해 "A"+"BC" 와 "AB"+"C" 가
    /// 같은 키가 되는 일을 막는다.
    ///
    /// <paramref name="owner"/> 에 null 을 넘기면 "테이블 이름만으로 맞추는 키"가 된다. 인덱스 추출
    /// 쿼리(all_ind_columns)처럼 OWNER 를 내보내지 않는 파일이 있어서, 그런 파일은 테이블 이름만으로
    /// 이어줄 수 있어야 하기 때문이다. (이 경우 서로 다른 스키마에 같은 이름의 테이블이 있으면
    /// 인덱스가 섞여 보일 수 있다 — 추출 쿼리에 OWNER 가 추가되면 자동으로 OWNER 까지 맞춘다)
    /// </summary>
    public static string Create(string? owner, string? tableName) =>
        $"{owner?.Trim().ToUpperInvariant()}\u0001{tableName?.Trim().ToUpperInvariant()}";
}

/// <summary>
/// JSON 레코드(필드 사전)에서 값을 꺼낼 때 쓰는 확장 메서드입니다.
/// 추출 도구/쿼리에 따라 같은 뜻의 필드가 다른 이름(TABLE_COMMENTS / COMMENTS 등)으로 나올 수 있어,
/// 후보 이름을 순서대로 찾아 처음 발견된 값을 돌려준다.
/// </summary>
public static class FieldLookupExtensions
{
    /// <summary>후보 필드명들을 순서대로 찾아, 값이 들어있는 첫 필드의 값을 반환한다. 없으면 null.</summary>
    public static string? GetValue(this IReadOnlyDictionary<string, string?> fields, params string[] candidateNames)
    {
        foreach (var name in candidateNames)
        {
            if (fields.TryGetValue(name, out var value) && !string.IsNullOrEmpty(value))
            {
                return value;
            }
        }

        return null;
    }
}
