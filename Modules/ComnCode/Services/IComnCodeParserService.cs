using HelperManager.Modules.ComnCode.Models;

namespace HelperManager.Modules.ComnCode.Services;

/// <summary>ComnCdInfo.json / ComnCdDetail.json 파일을 읽는 서비스의 계약입니다.</summary>
public interface IComnCodeParserService
{
    /// <summary>JSON 파일을 읽어 공통코드 레코드 목록으로 변환한다. 필드 구성은 미리 알 수 없으므로 전부 보존한다.</summary>
    IReadOnlyList<ComnCodeRecord> ParseFile(string filePath);
}
