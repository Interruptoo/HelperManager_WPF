using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using HelperManager.Modules.QueryStore.Models;

namespace HelperManager.Modules.QueryStore.Services;

/// <summary><see cref="IQueryStoreParserService"/> 의 실제 구현체입니다.</summary>
public sealed partial class QueryStoreParserService : IQueryStoreParserService
{
    // <sql id="..."> ~ </sql> 블록 전체(태그 포함)를 통째로 추출한다.
    // Singleline 옵션으로 '.' 이 개행 문자까지 포함해서 매칭하게 해서, 주석/본문이 여러 줄이어도 한 번에 잡는다.
    [GeneratedRegex("""<sql\s+id\s*=\s*"(?<id>[^"]+)"[^>]*>.*?</sql\s*>""", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex SqlBlockPattern();

    public IReadOnlyList<QueryDefinition> ParseFile(string filePath)
    {
        var text = File.ReadAllText(filePath, Encoding.UTF8);

        return SqlBlockPattern()
            .Matches(text)
            .Select(match => new QueryDefinition(
                WebUtility.HtmlDecode(match.Groups["id"].Value),
                // XML 문서 안에서는 SQL 본문에 들어있는 >, <, & 같은 문자가 &gt;/&lt;/&amp; 로
                // 이스케이프되어 있다. 화면에 보여주거나 클립보드로 복사할 때는 실제 기호
                // 그대로(>, <, & 등) 나와야 하므로, 블록을 읽어오는 시점에 한 번만 디코딩해서
                // 이후 모든 처리(화면 표시/SQL 가공/클립보드 복사)가 실제 문자를 다루게 한다.
                // <sql id="...">, </sql>, <!-- 주석 --> 처럼 원본에 실제 꺾쇠로 들어있는 태그는
                // 애초에 이스케이프되어 있지 않으므로 디코딩해도 영향을 받지 않는다.
                WebUtility.HtmlDecode(match.Value)))
            .ToList();
    }

    public IReadOnlyList<QueryTreeNode> BuildTree(IEnumerable<QueryDefinition> queries)
    {
        var roots = new List<QueryTreeNode>();

        foreach (var query in queries.OrderBy(q => q.Id, StringComparer.OrdinalIgnoreCase))
        {
            var segments = query.Id.Split('.');
            var currentLevel = roots;

            // id의 마지막 조각 전까지는 폴더 노드를 필요한 만큼 만들어가며 한 단계씩 내려간다.
            for (var i = 0; i < segments.Length - 1; i++)
            {
                var folder = GetOrAddFolder(currentLevel, segments[i]);
                currentLevel = folder.Children;
            }

            // 참고 화면처럼 리프 노드의 표시 텍스트는 마지막 조각이 아니라 id 전체를 그대로 사용한다.
            currentLevel.Add(new QueryTreeNode(query.Id, isLeaf: true, query));
        }

        return roots;
    }

    private static QueryTreeNode GetOrAddFolder(List<QueryTreeNode> siblings, string folderName)
    {
        var existing = siblings.FirstOrDefault(node => !node.IsLeaf && node.Name == folderName);
        if (existing is not null)
        {
            return existing;
        }

        var created = new QueryTreeNode(folderName, isLeaf: false);
        siblings.Add(created);
        return created;
    }
}
