using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelperManager.Modules.QueryStore.Models;
using HelperManager.Modules.QueryStore.Services;

namespace HelperManager.Modules.QueryStore;

/// <summary>
/// Query Store 화면에서 "열려 있는 쿼리 탭" 하나를 표현하는 ViewModel.
/// 트리에서 쿼리(리프 노드)를 더블클릭하거나 검색으로 찾으면 이 탭이 하나 만들어진다.
/// 쿼리 원본 텍스트/파라미터 값/클립보드 복사 결과까지, 탭마다 완전히 독립적인 상태를 가진다.
/// (반대로 트리/검색/파일 열기는 QueryStoreViewModel 쪽에 공용으로 남아있다.)
/// </summary>
public sealed partial class QueryTabViewModel : ObservableObject
{
    // 탭을 새로 열어도 같은 이름의 바인드 변수(예: HIS_HSP_TP_CD 처럼 여러 쿼리에 공통으로 쓰이는 값)는
    // 다시 입력하지 않도록, QueryStoreViewModel 이 모든 탭과 공유하는 사전을 그대로 전달받아 사용한다.
    private readonly Dictionary<string, string> _rememberedParameterValues;

    public QueryTabViewModel(QueryDefinition query, Dictionary<string, string> rememberedParameterValues)
    {
        Query = query;
        _rememberedParameterValues = rememberedParameterValues;

        title = query.Id.Split('.').LastOrDefault() is { Length: > 0 } shortName ? shortName : query.Id;
        detailText = query.Content;

        CopyToClipboardCommand = new RelayCommand(CopyToClipboard);
        FillParametersFromClipboardCommand = new RelayCommand(FillParametersFromClipboard, () => Parameters.Count > 0);
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));

        RebuildParameters();
    }

    /// <summary>이 탭이 보여주는 쿼리.</summary>
    public QueryDefinition Query { get; }

    /// <summary>탭 헤더에 표시되는 이름(쿼리 id의 마지막 조각).</summary>
    [ObservableProperty]
    private string title;

    /// <summary>쿼리 원본 텍스트. (XML 태그/주석 포함, 화면 확인용)</summary>
    [ObservableProperty]
    private string detailText;

    /// <summary>이 쿼리의 바인드 변수 목록. 값을 입력하면 클립보드 복사 시 치환된다.</summary>
    [ObservableProperty]
    private IReadOnlyList<QueryParameterItem> parameters = [];

    /// <summary>마지막으로 클립보드에 복사한 결과(치환 완료된 순수 SQL)를 보여주는 미리보기.</summary>
    [ObservableProperty]
    private string clipboardPreviewText = string.Empty;

    /// <summary>이 탭의 상태 메시지.</summary>
    [ObservableProperty]
    private string statusMessage = string.Empty;

    /// <summary>탭 닫기 버튼을 눌렀을 때 발생. QueryStoreViewModel 이 구독해서 OpenTabs 에서 제거한다.</summary>
    public event EventHandler? CloseRequested;

    public IRelayCommand CopyToClipboardCommand { get; }

    public IRelayCommand FillParametersFromClipboardCommand { get; }

    public IRelayCommand CloseCommand { get; }

    /// <summary>
    /// 쿼리 원본 텍스트에서 XML 겉포장을 걷어낸 뒤, 바인드 변수(:NAME)와 iBatis 조건 태그의
    /// Property 속성명을 모두 찾아 Parameters 목록을 구성한다. 이전에 입력했던 같은 이름의 값은
    /// (다른 탭/쿼리에서 입력한 것이라도) 자동으로 다시 채워준다.
    /// </summary>
    private void RebuildParameters()
    {
        var pureSql = SqlTextHelper.StripXmlWrapper(Query.Content);
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
    }

    /// <summary>파라미터 값을 입력할 때마다 "모든 탭이 공유하는 기억값" 사전을 갱신한다.</summary>
    private void OnParameterValueChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QueryParameterItem.Value) && sender is QueryParameterItem item)
        {
            _rememberedParameterValues[item.Name] = item.Value;
        }
    }

    /// <summary>
    /// 쿼리에서 XML 겉포장을 제거하고, iBatis 스타일 조건 태그(IsEqual/IsNotNull/Dynamic 등)를
    /// 입력된 파라미터 값 기준으로 평가해 조건에 맞는 SQL 조각만 남긴 뒤, 남은 바인드 변수를
    /// 입력된 값으로 치환해서 클립보드에 복사한다. (값이 비어있는 바인드 변수는 그대로 둔다)
    /// </summary>
    private void CopyToClipboard()
    {
        var pureSql = SqlTextHelper.StripXmlWrapper(Query.Content);

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
