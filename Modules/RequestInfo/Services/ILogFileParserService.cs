using HelperManager.Modules.RequestInfo.Models;

namespace HelperManager.Modules.RequestInfo.Services;

/// <summary>
/// 로그 폴더 탐색 및 JSON 로그 파일 파싱을 담당하는 서비스의 계약(인터페이스)입니다.
/// ViewModel 이 파일 시스템/파싱 세부 구현에 직접 의존하지 않도록 분리했습니다.
/// (테스트 시 가짜 구현으로 교체하거나, 추후 다른 형식의 로그 파서를 추가하기도 쉬워집니다.)
/// </summary>
public interface ILogFileParserService
{
    /// <summary>
    /// 지정한 폴더에서 지원 대상 확장자(.log, .txt, .clog)를 가진 파일 목록을 찾아
    /// 최근 수정 시각 순으로 정렬해서 반환한다.
    /// </summary>
    IReadOnlyList<LogFileInfo> FindLogFiles(string folderPath);

    /// <summary>
    /// 로그 파일 하나를 파싱한다. 파일 내용을 살펴 형식을 자동으로 판별한 뒤
    /// (JSON 라인 / ERROR 블록 / EQS_Trace 블록) 그에 맞는 파서로 위임한다.
    /// 세 형식 모두 아니라면 빈 결과를 반환하며, 이 경우 호출 측은 <see cref="ReadRawText"/> 로
    /// 원본 텍스트를 그대로 보여주는 최후의 대체 경로를 사용해야 한다.
    /// </summary>
    LogParseResult ParseLogFile(string filePath);

    /// <summary>
    /// 파싱된 로그 항목들에서 등장하는 모든 최상위 JSON 필드명을 중복 없이 모아 반환한다.
    /// (필드 필터 체크박스 목록을 동적으로 구성하기 위한 용도)
    /// </summary>
    IReadOnlyList<string> ExtractFieldNames(IEnumerable<LogEntry> entries);

    /// <summary>
    /// 지원하는 세 가지 형식 중 어느 것으로도 인식되지 않는 로그 파일을 위한 최후의 대체 경로.
    /// 파일 내용을 가공 없이(변환/필터링 없이) 그대로 문자열로 읽어 반환한다.
    /// </summary>
    string ReadRawText(string filePath);
}
