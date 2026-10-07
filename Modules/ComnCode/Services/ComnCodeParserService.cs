using HelperManager.Common;
using HelperManager.Modules.ComnCode.Models;

namespace HelperManager.Modules.ComnCode.Services;

/// <summary><see cref="IComnCodeParserService"/> 의 실제 구현체입니다.</summary>
public sealed class ComnCodeParserService : IComnCodeParserService
{
    public IReadOnlyList<ComnCodeRecord> ParseFile(string filePath) =>
        FlexibleJsonArrayParser.ParseFile(filePath)
            .Select(fields => new ComnCodeRecord { Fields = fields })
            .ToList();
}
