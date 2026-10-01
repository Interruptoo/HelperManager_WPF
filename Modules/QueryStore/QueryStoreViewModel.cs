using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelperManager.Modules.QueryStore.Models;
using HelperManager.Modules.QueryStore.Services;
using Microsoft.Win32;

namespace HelperManager.Modules.QueryStore;

/// <summary>
/// Query Store 화면(XML로 추출한 쿼리 모음 뷰어)의 ViewModel.
///
/// 화면 동작 흐름:
///  1) [파일 열기] 버튼으로 쿼리 모음 XML 파일을 고르면 -> 파일 안의 모든 &lt;sql id="..."&gt; 블록을
///     읽어 쿼리 목록을 만들고, id 의 점(.) 구조에 따라 트리(RootNodes)를 구성한다.
///  2) 트리에서 쿼리(리프) 노드를 선택하면(클릭/더블클릭 모두) -> 원본 텍스트(주석 헤더 + SQL 본문)를
///     상세 패널에 색상 강조와 함께 보여주고, 본문에 있는 :NAME 바인드 변수들을 찾아
///     오른쪽 파라미터 목록(Parameters)을 만든다.
///  3) 검색창에 쿼리 id 또는 SQL 본문 일부를 입력하고 [검색]을 누르면(라디오 버튼으로 둘 중 하나만
///     대상으로 검색) -> 일치하는 쿼리를 찾아 트리에서 상위 폴더를 모두 펼치고 선택 상태로 만든다.
///     일치 항목이 여러 개면 다시 눌러서 다음 항목으로 순서대로 이동할 수 있다.
///  4) 파라미터 값을 입력한 뒤 [클립보드 복사]를 누르면 -> DB 연결 없이, XML 겉포장을 걷어낸
///     순수 SQL 본문의 바인드 변수를 입력한 값으로 치환해 클립보드에 복사한다.
///     이 결과를 DBMS 도구(SQL Developer 등)에 붙여넣어 직접 실행하는 용도이다.
/// </summary>
public sealed partial class QueryStoreViewModel : ObservableObject
{
    private readonly IQueryStoreParserService _parserService;

    // 검색 결과(일치하는 리프 노드) 목록과, 그중 현재 몇 번째를 보고 있는지 가리키는 인덱스.
    private List<QueryTreeNode> _searchMatches = [];
    private int _searchMatchIndex = -1;

    // 쿼리를 바꿔 선택해도, 같은 이름의 바인드 변수(예: HIS_HSP_TP_CD, HIS_STF_NO 처럼 여러 쿼리에
    // 공통으로 쓰이는 값)는 다시 입력하지 않도록 세션 동안만 기억해두는 용도. (파일 저장 없음)
    private readonly Dictionary<string, string> _rememberedParameterValues = new(StringComparer.OrdinalIgnoreCase);

    public QueryStoreViewModel(IQueryStoreParserService parserService)
    {
        _parserService = parserService;

        OpenFileCommand = new RelayCommand(OpenFile);
        SearchCommand = new RelayCommand(SearchNext, () => !string.IsNullOrWhiteSpace(SearchKeyword) && RootNodes.Count > 0);
        CopyToClipboardCommand = new RelayCommand(CopyToClipboard, () => SelectedNode is { IsLeaf: true, Query: not null });
        FillParametersFromClipboardCommand = new RelayCommand(FillParametersFromClipboard, () => Parameters.Count > 0);
    }

    /// <summary>현재 불러온 XML 파일 경로.</summary>
    [ObservableProperty]
    private string? loadedFilePath;

    /// <summary>트리 최상위 노드 목록.</summary>
    [ObservableProperty]
    private IReadOnlyList<QueryTreeNode> rootNodes = [];

    /// <summary>검색창 입력값. 쿼리 id 또는 SQL 본문 일부를 입력해 트리에서 찾아 선택한다.</summary>
    [ObservableProperty]
    private string searchKeyword = string.Empty;

    /// <summary>true면 쿼리 id를 대상으로, false면 쿼리 내용(본문)을 대상으로 검색한다. (라디오 버튼)</summary>
    [ObservableProperty]
    private bool searchById = true;

    /// <summary>true면 쿼리 내용(본문)을 대상으로 검색한다. SearchById 와 라디오 버튼 그룹으로 묶여 배타적으로 동작한다.</summary>
    [ObservableProperty]
    private bool searchByContent;

    /// <summary>검색 결과 안내 ("3 / 12" 또는 "일치하는 쿼리가 없습니다." 등).</summary>
    [ObservableProperty]
    private string searchStatusText = string.Empty;

    /// <summary>현재 선택된 트리 노드. 코드비하인드의 TreeView.SelectedItemChanged 에서 채워진다.</summary>
    [ObservableProperty]
    private QueryTreeNode? selectedNode;

    /// <summary>상세 패널에 표시할 원본 쿼리 텍스트. (XML 태그/주석 포함, 화면 확인용)</summary>
    [ObservableProperty]
    private string detailText = string.Empty;

    /// <summary>선택된 쿼리의 바인드 변수 목록. 사용자가 값을 입력하면 클립보드 복사 시 치환된다.</summary>
    [ObservableProperty]
    private IReadOnlyList<QueryParameterItem> parameters = [];

    /// <summary>마지막으로 클립보드에 복사한 결과(치환 완료된 순수 SQL)를 보여주는 미리보기.</summary>
    [ObservableProperty]
    private string clipboardPreviewText = string.Empty;

    /// <summary>화면 하단 상태 메시지.</summary>
    [ObservableProperty]
    private string statusMessage = "쿼리 모음 XML 파일을 열어주세요.";

    public IRelayCommand OpenFileCommand { get; }

    public IRelayCommand SearchCommand { get; }

    public IRelayCommand CopyToClipboardCommand { get; }

    public IRelayCommand FillParametersFromClipboardCommand { get; }

    partial void OnSelectedNodeChanged(QueryTreeNode? value)
    {
        if (value is { IsLeaf: true, Query: not null })
        {
            DetailText = value.Query.Content;
            RebuildParameters(value.Query.Content);
        }
        else
        {
            DetailText = string.Empty;
            RebuildParameters(rawContent: null);
        }

        ClipboardPreviewText = string.Empty;
        CopyToClipboardCommand.NotifyCanExecuteChanged();
    }

    partial void OnSearchKeywordChanged(string value)
    {
        SearchCommand.NotifyCanExecuteChanged();
        ResetSearchState();
    }

    partial void OnSearchByIdChanged(bool value) => ResetSearchState();

    partial void OnSearchByContentChanged(bool value) => ResetSearchState();

    /// <summary>검색어/검색 대상(라디오 버튼)이 바뀌면 이전 검색의 "몇 번째 결과" 순번은 의미가 없어지므로 초기화한다.</summary>
    private void ResetSearchState()
    {
        _searchMatches = [];
        _searchMatchIndex = -1;
        SearchStatusText = string.Empty;
    }

    /// <summary>Windows 파일 열기 대화상자를 띄워 쿼리 모음 XML 파일을 고른다.</summary>
    private void OpenFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "쿼리 모음 XML 파일을 선택하세요",
            Filter = "XML 파일 (*.xml)|*.xml|모든 파일 (*.*)|*.*",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        LoadedFilePath = dialog.FileName;

        var queries = _parserService.ParseFile(dialog.FileName);
        RootNodes = _parserService.BuildTree(queries);

        SelectedNode = null;
        SearchKeyword = string.Empty;
        ResetSearchState();

        StatusMessage = queries.Count == 0
            ? "이 파일에서 <sql id=\"...\"> 형식의 쿼리를 찾지 못했습니다."
            : $"{queries.Count}개의 쿼리를 불러왔습니다.";

        SearchCommand.NotifyCanExecuteChanged();
        CopyToClipboardCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 검색어와 일치하는(라디오 버튼으로 고른 대상: id 또는 본문) 쿼리를 찾아 트리에서 선택 상태로 만든다.
    /// 같은 검색어로 다시 실행하면 일치 항목을 순서대로 순환하며 보여준다.
    /// </summary>
    private void SearchNext()
    {
        if (_searchMatches.Count == 0)
        {
            _searchMatches = CollectLeaves(RootNodes)
                .Where(node => Matches(node, SearchKeyword))
                .ToList();
            _searchMatchIndex = -1;
        }

        if (_searchMatches.Count == 0)
        {
            SearchStatusText = "일치하는 쿼리가 없습니다.";
            return;
        }

        _searchMatchIndex = (_searchMatchIndex + 1) % _searchMatches.Count;
        SelectInTree(_searchMatches[_searchMatchIndex]);

        SearchStatusText = $"{_searchMatchIndex + 1} / {_searchMatches.Count}";
    }

    /// <summary>라디오 버튼으로 고른 대상(쿼리 id 또는 쿼리 내용)만을 기준으로 일치 여부를 판단한다.</summary>
    private bool Matches(QueryTreeNode node, string keyword)
    {
        if (node.Query is null)
        {
            return false;
        }

        return SearchById
            ? node.Query.Id.Contains(keyword, StringComparison.OrdinalIgnoreCase)
            : node.Query.Content.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<QueryTreeNode> CollectLeaves(IEnumerable<QueryTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.IsLeaf)
            {
                yield return node;
            }
            else
            {
                foreach (var leaf in CollectLeaves(node.Children))
                {
                    yield return leaf;
                }
            }
        }
    }

    /// <summary>찾은 노드까지 이어지는 모든 상위 폴더를 펼치고, 해당 노드를 선택 상태로 만든다.</summary>
    private void SelectInTree(QueryTreeNode target)
    {
        if (SelectedNode is not null)
        {
            SelectedNode.IsSelected = false;
        }

        ExpandAncestors(RootNodes, target);

        target.IsSelected = true;
        SelectedNode = target; // DetailText/Parameters 갱신은 OnSelectedNodeChanged 에서 처리된다.
    }

    /// <summary>target 까지의 경로에 있는 모든 폴더 노드의 IsExpanded 를 true 로 만든다.</summary>
    private static bool ExpandAncestors(IEnumerable<QueryTreeNode> nodes, QueryTreeNode target)
    {
        foreach (var node in nodes)
        {
            if (ReferenceEquals(node, target))
            {
                return true;
            }

            if (!node.IsLeaf && ExpandAncestors(node.Children, target))
            {
                node.IsExpanded = true;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 선택된 쿼리의 원본 텍스트에서 XML 겉포장을 걷어낸 뒤, 바인드 변수(:NAME)와
    /// iBatis 스타일 조건 태그의 Property 속성명을 모두 찾아 Parameters 목록을 구성한다.
    /// (Property 는 실제 바인드로는 안 쓰이고 "어느 조건 블록을 포함할지"만 결정하는 경우도 있어서,
    ///  바인드 변수만 봐서는 찾을 수 없는 입력 항목까지 함께 모아준다.)
    /// 이전에 입력했던 같은 이름의 값은 자동으로 다시 채워준다.
    /// </summary>
    private void RebuildParameters(string? rawContent)
    {
        foreach (var old in Parameters)
        {
            old.PropertyChanged -= OnParameterValueChanged;
        }

        if (rawContent is null)
        {
            Parameters = [];
            FillParametersFromClipboardCommand.NotifyCanExecuteChanged();
            return;
        }

        var pureSql = SqlTextHelper.StripXmlWrapper(rawContent);
        var bindNames = SqlTextHelper.ExtractBindVariableNames(pureSql);
        var propertyNames = DynamicSqlResolver.ExtractPropertyNames(pureSql);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = new List<QueryParameterItem>();
        foreach (var name in bindNames.Concat(propertyNames))
        {
            if (!seen.Add(name))
            {
                continue; // 바인드 변수와 조건 Property 이름이 같으면(흔한 경우) 중복 추가하지 않는다.
            }

            var item = new QueryParameterItem(name);
            if (_rememberedParameterValues.TryGetValue(name, out var remembered))
            {
                item.Value = remembered;
            }

            item.PropertyChanged += OnParameterValueChanged;
            items.Add(item);
        }

        Parameters = items;
        FillParametersFromClipboardCommand.NotifyCanExecuteChanged();
    }

    /// <summary>파라미터 값을 입력할 때마다 "세션 동안 기억해둘 값" 사전을 갱신한다.</summary>
    private void OnParameterValueChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QueryParameterItem.Value) && sender is QueryParameterItem item)
        {
            _rememberedParameterValues[item.Name] = item.Value;
        }
    }

    /// <summary>
    /// 선택된 쿼리에서 XML 겉포장을 제거하고, iBatis 스타일 조건 태그(IsEqual/IsNotNull/Dynamic 등)를
    /// 입력된 파라미터 값 기준으로 평가해 조건에 맞는 SQL 조각만 남긴 뒤, 남은 바인드 변수를
    /// 입력된 값으로 치환해서 클립보드에 복사한다. (값이 비어있는 바인드 변수는 그대로 둔다)
    /// </summary>
    private void CopyToClipboard()
    {
        if (SelectedNode is not { IsLeaf: true, Query: not null })
        {
            return;
        }

        var pureSql = SqlTextHelper.StripXmlWrapper(SelectedNode.Query.Content);

        var parameterValueLookup = Parameters.ToDictionary(p => p.Name, p => p.Value, StringComparer.OrdinalIgnoreCase);
        var resolvedSql = DynamicSqlResolver.Resolve(pureSql, parameterValueLookup);

        var substitutions = Parameters
            .Where(parameter => !string.IsNullOrEmpty(parameter.Value))
            .Select(parameter => (parameter.Name, Replacement: FormatForSubstitution(parameter)));

        var substitutionList = substitutions.ToList();
        var finalSql = SqlTextHelper.SubstituteBindVariables(resolvedSql, substitutionList);

        Clipboard.SetText(finalSql);
        ClipboardPreviewText = finalSql;

        StatusMessage = $"클립보드에 복사했습니다. (바인드 변수 {substitutionList.Count}개 치환)";
    }

    /// <summary>
    /// 클립보드의 텍스트(RequestInfo 요청 로그 뷰어에서 복사한 "■ RequestContent { JSON... }" 같은
    /// 내용)를 읽어, 그 안의 JSON 키와 이름이 같은 파라미터에 값(및 추정 타입)을 자동으로 채운다.
    /// 이름이 일치하지 않는 파라미터는 건드리지 않는다.
    /// </summary>
    private void FillParametersFromClipboard()
    {
        string clipboardText;
        try
        {
            clipboardText = Clipboard.GetText();
        }
        catch (Exception)
        {
            // 다른 프로그램이 클립보드를 잠깐 점유하고 있는 등 드문 경우를 조용히 무시한다.
            StatusMessage = "클립보드 내용을 읽지 못했습니다. 잠시 후 다시 시도해주세요.";
            return;
        }

        if (string.IsNullOrWhiteSpace(clipboardText))
        {
            StatusMessage = "클립보드가 비어 있습니다.";
            return;
        }

        var values = RequestLogValueExtractor.ExtractFlatValues(clipboardText);
        if (values.Count == 0)
        {
            StatusMessage = "클립보드 내용에서 JSON 값을 찾지 못했습니다.";
            return;
        }

        var filledCount = 0;
        foreach (var parameter in Parameters)
        {
            if (!values.TryGetValue(parameter.Name, out var parsed))
            {
                continue;
            }

            parameter.Value = parsed.Value;
            parameter.ValueType = parsed.Type;
            filledCount++;
        }

        StatusMessage = filledCount > 0
            ? $"클립보드 내용으로 파라미터 {filledCount}개를 채웠습니다."
            : "클립보드 내용과 이름이 일치하는 파라미터가 없습니다.";
    }

    /// <summary>파라미터의 데이터 타입(문자열/숫자/날짜)에 맞춰 SQL에 들어갈 리터럴 형태로 값을 가공한다.</summary>
    private static string FormatForSubstitution(QueryParameterItem parameter) => parameter.ValueType switch
    {
        ParameterValueType.Number => parameter.Value,
        ParameterValueType.Date => FormatDateLiteral(parameter.Value),
        _ => QuoteSqlLiteral(parameter.Value),
    };

    /// <summary>
    /// 날짜 값을 Oracle TO_DATE(...) 리터럴로 감싼다. 값에 ':' 가 포함되어 있으면(시:분:초)
    /// 시간까지 포함한 포맷을, 아니면 날짜만 있는 포맷을 사용한다.
    /// </summary>
    private static string FormatDateLiteral(string value)
    {
        var trimmed = value.Trim();
        var format = trimmed.Contains(':') ? "YYYY-MM-DD HH24:MI:SS" : "YYYY-MM-DD";
        return $"TO_DATE({QuoteSqlLiteral(trimmed)}, '{format}')";
    }

    private static string QuoteSqlLiteral(string value) => $"'{value.Replace("'", "''")}'";
}
