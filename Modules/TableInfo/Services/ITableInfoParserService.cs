using HelperManager.Modules.TableInfo.Models;

namespace HelperManager.Modules.TableInfo.Services;

/// <summary>
/// TableInfo 화면이 사용하는 네 종류의 JSON 파일(테이블 / 컬럼 / 인덱스 / 사용 오브젝트)을 읽는
/// 서비스의 계약입니다. 파일마다 형태가 같고(레코드 배열) 꺼낼 모델만 다르므로 메서드를 나눠 둔다.
/// </summary>
public interface ITableInfoParserService
{
    /// <summary>TableInfo JSON 파일을 읽어 테이블 목록으로 변환한다.</summary>
    IReadOnlyList<TableInfoEntry> ParseTables(string filePath);

    /// <summary>ColumnInfo JSON 파일을 읽어 컬럼 목록으로 변환한다.</summary>
    IReadOnlyList<ColumnInfoEntry> ParseColumns(string filePath);

    /// <summary>IndexInfo JSON 파일을 읽어 인덱스 목록으로 변환한다.</summary>
    IReadOnlyList<IndexInfoEntry> ParseIndexes(string filePath);

    /// <summary>TableUseObjectList JSON 파일을 읽어 "테이블을 사용 중인 오브젝트" 목록으로 변환한다.</summary>
    IReadOnlyList<TableObjectEntry> ParseObjects(string filePath);
}
