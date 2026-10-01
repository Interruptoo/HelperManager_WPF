namespace HelperManager.Modules.RequestInfo.Models;

/// <summary>
/// 로그 파일 하나를 파싱한 결과입니다.
/// ERROR_*.log, EQS_Trace_*.log 같이 한 줄 JSON 형식이 아닌 파일을 실수로 선택했을 때도
/// 사용자에게 상황(총 줄 수 대비 건너뛴 줄 수)을 알려줄 수 있도록 통계 정보를 함께 담는다.
/// </summary>
/// <param name="Entries">JSON으로 정상 파싱된 로그 항목 목록.</param>
/// <param name="SkippedLineCount">JSON 파싱에 실패해 건너뛴 줄 수 (스택 트레이스 등 비-JSON 라인).</param>
/// <param name="TotalLineCount">공백이 아닌 전체 줄 수.</param>
public sealed record LogParseResult(IReadOnlyList<LogEntry> Entries, int SkippedLineCount, int TotalLineCount);
