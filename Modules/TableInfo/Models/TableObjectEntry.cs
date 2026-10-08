namespace HelperManager.Modules.TableInfo.Models;

/// <summary>
/// TableUseObjectList JSON 파일에서 읽어온, 테이블을 사용 중인 오브젝트 한 개입니다.
/// (프로시저/트리거/뷰 등 — 선택한 테이블을 참조하는 오브젝트 목록에 표시)
/// </summary>
public sealed class TableObjectEntry
{
    /// <summary>원본 JSON 객체의 모든 필드. 키는 대소문자를 구분하지 않고 찾을 수 있다.</summary>
    public required IReadOnlyDictionary<string, string?> Fields { get; init; }

    /// <summary>오브젝트의 소유자. (참조 "대상" 테이블의 소유자가 아니라 오브젝트 쪽 소유자다)</summary>
    public string? Owner => Fields.GetValue("OWNER", "OBJ_OWNER");

    public string? ObjName => Fields.GetValue("OBJ_NAME", "OBJECT_NAME", "NAME");

    public string? ObjType => Fields.GetValue("OBJ_TYPE", "OBJECT_TYPE", "TYPE");

    public string? Status => Fields.GetValue("STATUS");

    /// <summary>
    /// 이 오브젝트가 사용(참조)하는 테이블 이름. TableInfo 목록에서 테이블을 고르면 이 값으로 걸러낸다.
    /// 추출 쿼리는 all_dependencies 쪽은 REFERENCED_NAME, 트리거 쪽은 TABLE_NAME 으로 내보내므로
    /// 둘 다 후보에 둔다. (이 값이 아예 없는 파일이면 테이블별로 걸러낼 수 없어 화면에서 안내한다)
    /// </summary>
    public string? UsedTableName =>
        Fields.GetValue("TABLE_NAME", "REFERENCED_NAME", "REF_TABLE_NAME", "USE_TABLE_NAME");

    /// <summary>
    /// 사용 중인 테이블의 소유자. 추출 쿼리가 referenced_owner 를 내보내지 않으면 비어 있다.
    ///
    /// ※ 비어 있을 때 오브젝트 자신의 OWNER 로 대신하면 안 된다. 예를 들어 HBIL 의 프로시저가
    ///   HMED 의 테이블을 참조하는 경우가 흔해서, 오브젝트 소유자로 넘겨짚으면 그런 참조가 전부
    ///   누락된다(실제 데이터에서 4,771건 중 348건만 연결됐다). 그래서 소유자를 모를 때는
    ///   <see cref="TableOnlyKey"/> 로 테이블 이름만 맞춘다.
    /// </summary>
    public string? UsedTableOwner => Fields.GetValue("TABLE_OWNER", "REFERENCED_OWNER", "REF_OWNER");

    /// <summary>참조하는 테이블을 가리키는 조인 키(OWNER + TABLE_NAME). 파일에 참조 소유자가 있을 때만 쓴다.</summary>
    public string JoinKey => TableKey.Create(UsedTableOwner, UsedTableName);

    /// <summary>OWNER 없이 테이블 이름만으로 맞추는 키. 현재 추출 쿼리에는 참조 소유자가 없어 이쪽을 쓴다.</summary>
    public string TableOnlyKey => TableKey.Create(null, UsedTableName);
}
