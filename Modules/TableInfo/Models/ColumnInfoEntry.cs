namespace HelperManager.Modules.TableInfo.Models;

/// <summary>ColumnInfo JSON 파일에서 읽어온 컬럼 한 개입니다. (선택한 테이블의 컬럼 목록에 표시)</summary>
public sealed class ColumnInfoEntry
{
    /// <summary>원본 JSON 객체의 모든 필드. 키는 대소문자를 구분하지 않고 찾을 수 있다.</summary>
    public required IReadOnlyDictionary<string, string?> Fields { get; init; }

    public string? Owner => Fields.GetValue("OWNER");

    public string? TableName => Fields.GetValue("TABLE_NAME");

    public string? ColName => Fields.GetValue("COL_NAME", "COLUMN_NAME");

    /// <summary>NULL 허용 여부("Y"/"N").</summary>
    public string? Nullable => Fields.GetValue("NULLABLE");

    /// <summary>길이/정밀도까지 포함된 타입 표기(예: VARCHAR2(2)).</summary>
    public string? DataType => Fields.GetValue("DATATYPE", "DATA_TYPE");

    public string? Comments => Fields.GetValue("COMMENTS", "COL_COMMENTS");

    /// <summary>기본값(DEFAULT). 지정되지 않은 컬럼이 대부분이라 보통 비어 있다.</summary>
    public string? DefaultValue => Fields.GetValue("DEFAULT", "DATA_DEFAULT");

    /// <summary>소속 테이블을 가리키는 조인 키(OWNER + TABLE_NAME).</summary>
    public string JoinKey => TableKey.Create(Owner, TableName);
}
