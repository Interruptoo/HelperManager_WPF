using CommunityToolkit.Mvvm.ComponentModel;

namespace HelperManager.Modules.QueryStore.Models;

/// <summary>
/// 선택된 쿼리의 바인드 변수(:NAME) 하나에 대해, 사용자가 입력할 치환 값을 표현합니다.
/// DB 연결 없이 "클립보드 복사" 로 값을 채운 완성된 SQL 문을 만들어 DBMS 도구에 붙여넣어
/// 실행할 수 있게 하기 위한 용도이다.
/// </summary>
public sealed partial class QueryParameterItem : ObservableObject
{
    /// <summary>콤보박스에 표시할 데이터 타입 전체 목록. (x:Static 으로 XAML 에서 바로 바인딩한다)</summary>
    public static IReadOnlyList<ParameterValueType> AllValueTypes { get; } = Enum.GetValues<ParameterValueType>();

    public QueryParameterItem(string name)
    {
        Name = name;
    }

    /// <summary>바인드 변수 이름. (콜론 제외, 예: "STF_NO")</summary>
    public string Name { get; }

    /// <summary>사용자가 입력한 치환 값. 비어 있으면 치환하지 않고 바인드 변수를 그대로 둔다.</summary>
    [ObservableProperty]
    private string value = string.Empty;

    /// <summary>값을 SQL에 치환할 때의 형태(문자열/숫자/날짜). 콤보박스로 선택한다.</summary>
    [ObservableProperty]
    private ParameterValueType valueType = ParameterValueType.Text;
}
