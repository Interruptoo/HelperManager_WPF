using HelperManager.Modules.QueryStore.Models;

namespace HelperManager.Modules.QueryStore.Services;

/// <summary>
/// XML로 추출된 쿼리 모음 파일을 읽어 쿼리 목록/트리로 변환하는 서비스의 계약입니다.
/// </summary>
public interface IQueryStoreParserService
{
    /// <summary>
    /// 파일에서 &lt;sql id="..."&gt;...&lt;/sql&gt; 블록을 모두 찾아 쿼리 목록으로 반환한다.
    /// 전체 파일을 엄격한 XML 로 파싱하지 않고 블록 단위로 원본 텍스트를 그대로 추출하므로,
    /// 주석/들여쓰기 같은 원본 서식이 그대로 보존되고, 본문에 이스케이프되지 않은 특수문자가
    /// 섞여 있어도(완전한 XML 문서가 아니어도) 안전하게 읽을 수 있다.
    /// </summary>
    IReadOnlyList<QueryDefinition> ParseFile(string filePath);

    /// <summary>쿼리 목록을 id의 점(.) 구분 계층 구조에 따라 트리(폴더+리프) 형태로 구성한다.</summary>
    IReadOnlyList<QueryTreeNode> BuildTree(IEnumerable<QueryDefinition> queries);
}
