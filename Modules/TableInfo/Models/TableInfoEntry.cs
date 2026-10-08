namespace HelperManager.Modules.TableInfo.Models;

/// <summary>
/// TableInfo JSON 파일에서 읽어온 테이블 한 개입니다.
///
/// 원본 필드는 <see cref="Fields"/> 에 대소문자 구분 없이 그대로 보관하고, 화면에 고정 컬럼으로
/// 빠르게 보여줄 필드만 이름 있는 속성으로 노출한다. (ComnCode 화면과 같은 방식 — 동적 컬럼 생성은
/// 필터링할 때마다 테이블을 다시 만들어야 해서 느리다)
/// </summary>
public sealed class TableInfoEntry
{
    /// <summary>원본 JSON 객체의 모든 필드. 키는 대소문자를 구분하지 않고 찾을 수 있다.</summary>
    public required IReadOnlyDictionary<string, string?> Fields { get; init; }

    public string? Owner => Fields.GetValue("OWNER");

    public string? TableName => Fields.GetValue("TABLE_NAME");

    public string? TableComments => Fields.GetValue("TABLE_COMMENTS", "COMMENTS");

    /// <summary>테이블이 생성된 지 며칠 지났는지. (목록 정렬/확인용으로 툴팁에만 쓴다)</summary>
    public string? CreatedDays => Fields.GetValue("CREATED_DAYS");

    /// <summary>테이블이 마지막으로 변경된 지 며칠 지났는지.</summary>
    public string? ModifyDays => Fields.GetValue("MODIFY_DAYS");

    /// <summary>ColumnInfo/IndexInfo/Object 목록을 이 테이블로 좁힐 때 쓰는 조인 키(OWNER + TABLE_NAME).</summary>
    public string JoinKey => TableKey.Create(Owner, TableName);

    /// <summary>
    /// OWNER 없이 테이블 이름만으로 맞추는 키. 인덱스 추출 쿼리(all_ind_columns)처럼 OWNER 를
    /// 내보내지 않는 파일과 이어줄 때 쓴다. 자세한 사정은 <see cref="TableKey"/> 참고.
    /// </summary>
    public string TableOnlyKey => TableKey.Create(null, TableName);
}
