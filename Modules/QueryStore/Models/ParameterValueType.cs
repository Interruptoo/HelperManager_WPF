namespace HelperManager.Modules.QueryStore.Models;

/// <summary>
/// 파라미터 값을 SQL 문자열에 치환할 때 어떤 형태로 다룰지를 나타낸다.
/// 콤보박스로 사용자가 직접 선택한다.
/// </summary>
public enum ParameterValueType
{
    /// <summary>작은따옴표로 감싼 문자열 리터럴로 치환한다. (예: 'ABC')</summary>
    Text,

    /// <summary>따옴표 없이 숫자 그대로 치환한다. (예: 123)</summary>
    Number,

    /// <summary>TO_DATE(...) 로 감싼 날짜 리터럴로 치환한다. (예: TO_DATE('2026-09-22','YYYY-MM-DD'))</summary>
    Date,
}
