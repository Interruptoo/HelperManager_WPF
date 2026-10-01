namespace HelperManager.Modules.MenuInfo.Models;

/// <summary>
/// 정기적으로 추출되는 메뉴 정보 JSON 파일에서 읽어온 메뉴 한 줄입니다.
/// 원본 JSON에는 더 많은 필드가 있을 수 있지만, 화면에는 필요한 8개 필드만 보여준다.
/// </summary>
public sealed record MenuInfoEntry(
    string? UseYn,
    string? DispYn,
    string? MenuNm,
    string? MenuCd,
    string? AssemblyNm,
    string? AppUrl,
    string? Depth1,
    string? Depth2);
