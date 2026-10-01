using HelperManager.Modules.MenuInfo.Models;

namespace HelperManager.Modules.MenuInfo.Services;

/// <summary>정기적으로 추출되는 메뉴 정보 JSON 파일을 읽는 서비스의 계약입니다.</summary>
public interface IMenuInfoParserService
{
    /// <summary>
    /// JSON 파일을 읽어 메뉴 목록으로 변환한다. 파일의 최상위가 배열이든, 배열을 속성으로 감싼
    /// 객체(예: {"items": [...]})든 모두 처리하도록, 최상위에서 배열을 찾아 그 안의 각 객체를
    /// 필요한 8개 필드만 뽑아 읽는다. 필요한 필드가 없거나 형식이 다른 값이어도 예외 없이
    /// 비어있는 값으로 처리한다.
    /// </summary>
    IReadOnlyList<MenuInfoEntry> ParseFile(string filePath);
}
