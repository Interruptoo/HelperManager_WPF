using System.Text.Json.Nodes;

namespace HelperManager.Modules.RequestInfo.Models;

/// <summary>
/// 로그 파일에서 파싱된 "항목 하나"를 표현하는 공통 모델입니다.
/// 이 화면은 서로 다른 세 가지 로그 형식을 다룹니다.
///   1) chisservice_inout_*.txt/.clog : 한 줄짜리 JSON (요청/응답)      -> JsonRoot 에 값이 채워진다.
///   2) ERROR_*.log                   : "[시간] Type:.. | ... | Error:.." 헤더 + 여러 줄 본문,
///                                       "===...===" 구분선으로 항목이 나뉜다.               -> PlainDetail 사용.
///   3) EQS_Trace_*.log               : "클래스,메서드, ..., 시간, 예외" 헤더 + 스택 트레이스   -> PlainDetail 사용.
/// JSON 형식만 필드 단위 체크박스 필터링이 가능하고, 나머지 두 형식은 파싱 시점에 미리
/// 사람이 읽기 좋은 형태로 가공해둔 PlainDetail 을 그대로 보여준다.
/// </summary>
public sealed class LogEntry
{
    /// <summary>파일 내에서 이 항목이 시작되는 줄 번호 (1부터 시작).</summary>
    public required int LineNumber { get; init; }

    /// <summary>파싱 전 원본 텍스트. JSON 로그는 한 줄, 블록형 로그는 블록 전체이며 검색(키워드 필터)에 사용된다.</summary>
    public required string RawText { get; init; }

    /// <summary>항목 구분값. 예: "Request"/"Response"(JSON), "ERROR"(오류 로그), "EQS_TRACE"(SQL 예외 트레이스).</summary>
    public string? Category { get; init; }

    /// <summary>마스터 목록(그리드)에 표시할 한 줄 요약. (JSON은 API 경로, 그 외는 오류/예외 메시지 요약)</summary>
    public string? Summary { get; init; }

    /// <summary>요청-응답 쌍을 구분하는 상관관계 ID. (JSON 로그에만 존재)</summary>
    public string? ContextId { get; init; }

    /// <summary>HTTP 상태 코드. (JSON 응답 로그에만 존재)</summary>
    public string? HttpStatus { get; init; }

    /// <summary>항목이 기록된 시각.</summary>
    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>JSON 라인 로그일 때만 값이 채워진다. 필드 필터링/상세 렌더링에 사용된다.</summary>
    public JsonObject? JsonRoot { get; init; }

    /// <summary>JSON이 아닌 로그일 때, 파싱 단계에서 미리 가공해 둔 가독성 좋은 상세 텍스트.</summary>
    public string? PlainDetail { get; init; }
}
