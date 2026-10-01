using CommunityToolkit.Mvvm.ComponentModel;

namespace HelperManager.Modules.QueryStore.Models;

/// <summary>
/// Query Store 화면의 TreeView 한 칸을 표현합니다.
/// 쿼리 id(예: "HIS.MS.CS.PH.CO.InsertClosingDegree")를 점(.) 기준으로 쪼개서
/// 마지막 조각 전까지는 폴더(패키지 경로) 노드로, 마지막 조각은 쿼리 자체를 가리키는
/// 리프 노드로 트리를 구성한다. (참고 화면처럼 리프 노드의 표시 텍스트는 마지막 조각이 아니라
/// id 전체를 그대로 사용한다.)
/// </summary>
public sealed partial class QueryTreeNode : ObservableObject
{
    public QueryTreeNode(string name, bool isLeaf, QueryDefinition? query = null)
    {
        Name = name;
        IsLeaf = isLeaf;
        Query = query;
    }

    /// <summary>트리에 표시되는 이름. 폴더 노드는 id 조각 하나, 리프 노드는 쿼리 전체 id.</summary>
    public string Name { get; }

    /// <summary>true면 쿼리 자체(리프), false면 폴더(패키지 경로의 한 단계).</summary>
    public bool IsLeaf { get; }

    /// <summary>IsLeaf 가 true 일 때만 값이 채워지는 실제 쿼리 데이터.</summary>
    public QueryDefinition? Query { get; }

    /// <summary>하위 노드 목록. 트리를 만들 때 한 번 채워지고 이후에는 바뀌지 않는다.</summary>
    public List<QueryTreeNode> Children { get; } = [];

    /// <summary>검색으로 찾아간 노드까지 상위 폴더들을 자동으로 펼치기 위한 바인딩 속성.</summary>
    [ObservableProperty]
    private bool isExpanded;

    /// <summary>검색 결과를 TreeView 에서 선택 상태로 표시하기 위한 바인딩 속성.</summary>
    [ObservableProperty]
    private bool isSelected;
}
