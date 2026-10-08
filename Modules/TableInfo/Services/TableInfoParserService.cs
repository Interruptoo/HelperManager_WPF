using HelperManager.Common;
using HelperManager.Modules.TableInfo.Models;

namespace HelperManager.Modules.TableInfo.Services;

/// <summary>
/// <see cref="ITableInfoParserService"/> 의 실제 구현체입니다.
/// 네 파일 모두 "레코드 배열" 형태이므로, 다른 화면들과 같은 공용 파서
/// (<see cref="FlexibleJsonArrayParser"/>)로 필드 사전을 만든 뒤 모델로만 감싼다.
/// 이렇게 하면 추출 쿼리에 필드가 추가되어도 파서를 고칠 필요가 없다.
/// </summary>
public sealed class TableInfoParserService : ITableInfoParserService
{
    public IReadOnlyList<TableInfoEntry> ParseTables(string filePath) =>
        FlexibleJsonArrayParser.ParseFile(filePath)
            .Select(fields => new TableInfoEntry { Fields = fields })
            .ToList();

    public IReadOnlyList<ColumnInfoEntry> ParseColumns(string filePath) =>
        FlexibleJsonArrayParser.ParseFile(filePath)
            .Select(fields => new ColumnInfoEntry { Fields = fields })
            .ToList();

    public IReadOnlyList<IndexInfoEntry> ParseIndexes(string filePath) =>
        FlexibleJsonArrayParser.ParseFile(filePath)
            .Select(fields => new IndexInfoEntry { Fields = fields })
            .ToList();

    public IReadOnlyList<TableObjectEntry> ParseObjects(string filePath) =>
        FlexibleJsonArrayParser.ParseFile(filePath)
            .Select(fields => new TableObjectEntry { Fields = fields })
            .ToList();
}
