using HelperManager.Modules.ComnCode.Models;

namespace HelperManager.Modules.ComnCode.Services;

/// <summary>
/// 공통코드 파일(ComnCdInfo.json / ComnCdDetail.json)을 읽어 앱 전체가 함께 쓰는 보관소입니다.
///
/// 이 보관소를 따로 둔 이유는 메모리 때문이다. ComnCdDetail.json 은 16만 건이 넘어서 한 번
/// 읽어 들이면 약 500MB 를 차지한다. Common Code 화면과 Table Info 화면이 각자 읽으면 1GB 가
/// 되므로, 한 벌만 읽어 두 화면이 나눠 쓴다.
///
/// 읽는 시점은 "처음 필요해질 때"다. Info 와 Detail 을 따로 읽어서, 그룹 목록(작다)만 필요한
/// 화면이 상세 목록(크다)까지 억지로 읽지 않도록 한다.
/// </summary>
public interface IComnCodeDataProvider
{
    /// <summary>공통코드 그룹 목록(ComnCdInfo.json). 처음 접근할 때 읽어 캐시한다.</summary>
    IReadOnlyList<ComnCodeRecord> Groups { get; }

    /// <summary>공통코드 상세 목록(ComnCdDetail.json). 처음 접근할 때 읽어 캐시한다. (약 500MB)</summary>
    IReadOnlyList<ComnCodeRecord> Details { get; }

    /// <summary>
    /// 캐시를 버린다. JSON 을 새로 추출한 뒤 호출하면, 다음에 필요해질 때 파일에서 다시 읽는다.
    /// </summary>
    void Invalidate();
}
