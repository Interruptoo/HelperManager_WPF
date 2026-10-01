using CommunityToolkit.Mvvm.ComponentModel;

namespace HelperManager.Modules.RequestInfo.Models;

/// <summary>
/// "내가 필요로 하는 body만 필터링해서 보고싶다"는 요구사항을 위한 체크박스 항목입니다.
/// 로그 파일에서 발견된 최상위 JSON 필드명(예: RequestContent, ResponseContent, Source 등)
/// 하나당 체크박스 하나가 대응되며, 체크된 필드만 상세 화면(오른쪽 패널)에 표시된다.
/// </summary>
public sealed partial class FieldFilterItem : ObservableObject
{
    public FieldFilterItem(string fieldName, bool isChecked)
    {
        FieldName = fieldName;
        this.isChecked = isChecked;
    }

    /// <summary>JSON 최상위 필드명. (예: "RequestContent")</summary>
    public string FieldName { get; }

    /// <summary>체크박스 선택 여부. true면 상세 화면에 이 필드를 표시한다.</summary>
    [ObservableProperty]
    private bool isChecked;
}
