namespace HelperManager.Modules.TableInfo.Models;

/// <summary>
/// IndexInfo JSON 파일에서 읽어온 인덱스 한 줄입니다. (선택한 테이블의 인덱스 목록에 표시)
///
/// 추출 쿼리(all_ind_columns + all_col_comments)는 TABLE_NAME / INDEX_NAME / COL_NAME / COMMENTS
/// 네 가지만 내보내고 OWNER 는 내보내지 않는다. 그래서 인덱스는 테이블 이름만으로 이어주며
/// (<see cref="TableOnlyKey"/>), 나중에 추출 쿼리에 OWNER 가 추가되면 그때는 OWNER 까지 맞춘다
/// (<see cref="JoinKey"/>). 어느 쪽을 쓸지는 TableInfoViewModel 이 파일을 읽을 때 자동으로 정한다.
/// </summary>
public sealed class IndexInfoEntry
{
    /// <summary>원본 JSON 객체의 모든 필드. 키는 대소문자를 구분하지 않고 찾을 수 있다.</summary>
    public required IReadOnlyDictionary<string, string?> Fields { get; init; }

    public string? Owner => Fields.GetValue("OWNER", "INDEX_OWNER", "TABLE_OWNER");

    public string? TableName => Fields.GetValue("TABLE_NAME");

    public string? IndexName => Fields.GetValue("INDEX_NAME", "IDX_NAME", "IDX_NM");

    /// <summary>인덱스를 구성하는 컬럼. 한 줄에 한 컬럼일 수도, 전체 컬럼이 묶여 있을 수도 있다.</summary>
    public string? ColumnName => Fields.GetValue("COLUMN_NAME", "COL_NAME", "COLUMN_LIST", "COLUMNS");

    public string? Comments => Fields.GetValue("COMMENTS", "INDEX_COMMENTS", "COMMENT");

    /// <summary>소속 테이블을 가리키는 조인 키(OWNER + TABLE_NAME). 파일에 OWNER 가 있을 때만 쓴다.</summary>
    public string JoinKey => TableKey.Create(Owner, TableName);

    /// <summary>OWNER 없이 테이블 이름만으로 맞추는 키. 현재 추출 쿼리에는 OWNER 가 없어 이쪽을 쓴다.</summary>
    public string TableOnlyKey => TableKey.Create(null, TableName);
}
