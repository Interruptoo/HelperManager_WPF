using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using HelperManager.Modules.RequestInfo.Models;

namespace HelperManager.Modules.RequestInfo.Services;

/// <summary>
/// <see cref="ILogFileParserService"/> 의 실제 구현체입니다.
///
/// 이 화면이 실제로 마주치는 로그 파일은 형식이 하나가 아닙니다.
///   1) chisservice_inout_*.txt/.clog : 한 줄짜리 JSON (요청/응답 바디)
///   2) ERROR_*.log                   : "[시간] Type:.. | Service:.. | ... | Error:.." 헤더 + 여러 줄 본문,
///                                       "================" 구분선으로 항목이 나뉜다.
///   3) EQS_Trace_*.log               : "클래스,메서드, ..., 시간, 예외" 헤더 한 줄 + 스택 트레이스 여러 줄.
/// 파일의 첫 줄 모양을 보고 형식을 자동 판별해서 알맞은 파서로 위임한다.
/// </summary>
public sealed partial class LogFileParserService : ILogFileParserService
{
    /// <summary>
    /// 이 화면이 다루는 로그 파일의 확장자 목록.
    /// chisservice_inout_*.txt 처럼 .txt 인 경우도 있고, chisservice_inout_*.clog 처럼
    /// 확장자가 .clog 인 경우도 있어서 둘 다(그리고 일반 .log 도) 지원 대상에 포함한다.
    /// </summary>
    private static readonly string[] SupportedExtensions = [".log", ".txt", ".clog"];

    // ERROR_*.log 항목 사이를 나누는 구분선. (예: "================================================================================")
    [GeneratedRegex(@"^=+$")]
    private static partial Regex SeparatorLinePattern();

    // ERROR_*.log 의 첫 줄(헤더) 형식.
    // 예) [2026-09-22 23:58:22.694] Type:ERROR | Service: | User:yuminho | IP:10.1.0.68 | Duration:0ms | Error: ...
    [GeneratedRegex(@"^\[(?<ts>[^\]]+)\]\s*Type:(?<type>[^|]*)\|\s*Service:(?<service>[^|]*)\|\s*User:(?<user>[^|]*)\|\s*IP:(?<ip>[^|]*)\|\s*Duration:(?<duration>[^|]*)\|\s*Error:(?<error>.*)$")]
    private static partial Regex ErrorBlockHeaderPattern();

    // EQS_Trace_*.log 의 항목 시작 줄 형식.
    // 예) HIS...UI.X,MethodA, HIS...BIZ.Y,MethodB, ..., 2026-09-22 오후 3:38:41, OracleException(...): 메시지
    [GeneratedRegex(@"^(?<chain>(?:[^,\r\n]+,\s*)+)(?<timestamp>\d{4}-\d{2}-\d{2}\s+(?:오전|오후)\s+\d{1,2}:\d{2}:\d{2}),\s*(?<exception>.+)$")]
    private static partial Regex EqsTraceHeaderPattern();

    public IReadOnlyList<LogFileInfo> FindLogFiles(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return [];
        }

        return Directory.EnumerateFiles(folderPath)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Select(path => new FileInfo(path))
            .OrderByDescending(info => info.LastWriteTime)
            .Select(info => new LogFileInfo(info.FullName, info.Name, info.CreationTime, info.LastWriteTime, info.Length))
            .ToList();
    }

    public LogParseResult ParseLogFile(string filePath)
    {
        var lines = File.ReadAllLines(filePath, Encoding.UTF8);

        var firstMeaningfulLine = lines.FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));
        if (firstMeaningfulLine is null)
        {
            return new LogParseResult([], 0, 0);
        }

        // 파일의 첫 줄 모양만 보고 세 가지 형식 중 하나로 판별한다.
        // (실제 로그는 파일 하나에 한 형식만 섞여 있다고 가정한다.)
        if (firstMeaningfulLine.TrimStart().StartsWith('{'))
        {
            return ParseJsonLines(lines);
        }

        if (ErrorBlockHeaderPattern().IsMatch(firstMeaningfulLine))
        {
            return ParseErrorBlocks(lines);
        }

        if (EqsTraceHeaderPattern().IsMatch(firstMeaningfulLine))
        {
            return ParseEqsTrace(lines);
        }

        // 세 형식 모두 아니면 빈 결과를 반환한다. 호출 측(ViewModel)이 ReadRawText 로 대체한다.
        return new LogParseResult([], 0, lines.Length);
    }

    public IReadOnlyList<string> ExtractFieldNames(IEnumerable<LogEntry> entries)
    {
        return entries
            .Where(entry => entry.JsonRoot is not null)
            .SelectMany(entry => entry.JsonRoot!.Select(property => property.Key))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    public string ReadRawText(string filePath) => File.ReadAllText(filePath, Encoding.UTF8);

    // ===================================================================
    // 1) chisservice_inout_*.txt/.clog : 한 줄 JSON (요청/응답)
    // ===================================================================

    private static LogParseResult ParseJsonLines(string[] lines)
    {
        var entries = new List<LogEntry>();
        var totalLineCount = 0;
        var skippedLineCount = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            totalLineCount++;

            var parsed = TryParseJsonObject(line);
            if (parsed is null)
            {
                // 이 파일은 JSON 라인 형식이라고 판단했지만, 개별 줄이 깨져있거나
                // 스택 트레이스가 섞여 들어간 경우 등은 조용히 건너뛴다.
                skippedLineCount++;
                continue;
            }

            entries.Add(BuildJsonEntry(i + 1, line, parsed));
        }

        return new LogParseResult(entries, skippedLineCount, totalLineCount);
    }

    private static JsonObject? TryParseJsonObject(string line)
    {
        if (line.Length == 0 || line[0] != '{')
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(line) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static LogEntry BuildJsonEntry(int lineNumber, string rawLine, JsonObject root)
    {
        var createdAtText = TryGetString(root, "CreateAt");
        DateTimeOffset? createdAt = createdAtText is not null && DateTimeOffset.TryParse(createdAtText, out var parsedDate)
            ? parsedDate
            : null;

        return new LogEntry
        {
            LineNumber = lineNumber,
            RawText = rawLine,
            Category = TryGetString(root, "LogCategory"),
            Summary = TryGetString(root, "Source"),
            ContextId = TryGetString(root, "ContextId"),
            HttpStatus = TryGetString(root, "HttpStatus"),
            Timestamp = createdAt,
            JsonRoot = root,
        };
    }

    private static string? TryGetString(JsonObject obj, string key)
    {
        if (obj.TryGetPropertyValue(key, out var node) && node is JsonValue value && value.TryGetValue(out string? text))
        {
            return text;
        }

        return null;
    }

    // ===================================================================
    // 2) ERROR_*.log : "[시간] Type:.. | ... | Error:.." 헤더 + 본문, "===" 구분선
    // ===================================================================

    private static LogParseResult ParseErrorBlocks(string[] lines)
    {
        var entries = new List<LogEntry>();
        var currentBlock = new List<string>();
        var blockStartLine = 0;
        var totalLineCount = 0;

        void FlushBlock()
        {
            if (currentBlock.Count == 0)
            {
                return;
            }

            entries.Add(BuildErrorEntry(blockStartLine, currentBlock));
            currentBlock = [];
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (SeparatorLinePattern().IsMatch(line.Trim()))
            {
                // 구분선을 만나면 지금까지 모은 줄을 하나의 항목으로 확정한다. (구분선 자체는 버린다)
                FlushBlock();
                continue;
            }

            if (string.IsNullOrWhiteSpace(line) && currentBlock.Count == 0)
            {
                continue; // 항목 시작 전의 빈 줄은 무시한다.
            }

            if (currentBlock.Count == 0)
            {
                blockStartLine = i + 1;
            }

            currentBlock.Add(line);
            totalLineCount++;
        }

        FlushBlock(); // 파일 끝에 구분선이 없는 마지막 항목 처리

        return new LogParseResult(entries, 0, totalLineCount);
    }

    private static LogEntry BuildErrorEntry(int startLineNumber, List<string> blockLines)
    {
        var headerMatch = ErrorBlockHeaderPattern().Match(blockLines[0]);
        var rawText = string.Join(Environment.NewLine, blockLines);

        if (!headerMatch.Success)
        {
            // 형식 판별은 통과했지만 개별 블록 헤더가 예상과 다르면, 원본을 그대로 보여준다.
            return new LogEntry
            {
                LineNumber = startLineNumber,
                RawText = rawText,
                Category = "ERROR",
                Summary = blockLines[0],
                PlainDetail = rawText,
            };
        }

        var timestampText = headerMatch.Groups["ts"].Value.Trim();
        var type = headerMatch.Groups["type"].Value.Trim();
        var service = headerMatch.Groups["service"].Value.Trim();
        var user = headerMatch.Groups["user"].Value.Trim();
        var ip = headerMatch.Groups["ip"].Value.Trim();
        var duration = headerMatch.Groups["duration"].Value.Trim();
        var firstErrorLine = headerMatch.Groups["error"].Value.Trim();

        DateTimeOffset? timestamp = DateTimeOffset.TryParse(timestampText, out var parsedTimestamp) ? parsedTimestamp : null;

        // 파이프(|)로 한 줄에 뭉쳐 있던 헤더 필드를 항목별로 줄바꿈해서 라벨을 붙이고,
        // 그 뒤에 원본 본문(URL/응답 코드/스택 등 여러 줄)을 그대로 이어붙인다.
        var detail = new StringBuilder();
        detail.AppendLine($"시간     : {timestampText}");
        detail.AppendLine($"유형     : {type}");
        if (!string.IsNullOrWhiteSpace(service))
        {
            detail.AppendLine($"서비스   : {service}");
        }
        detail.AppendLine($"사용자   : {user}");
        detail.AppendLine($"IP       : {ip}");
        detail.AppendLine($"소요시간 : {duration}");
        detail.AppendLine();
        detail.AppendLine("■ 오류 내용");
        detail.AppendLine(firstErrorLine);
        for (var i = 1; i < blockLines.Count; i++)
        {
            detail.AppendLine(blockLines[i]);
        }

        return new LogEntry
        {
            LineNumber = startLineNumber,
            RawText = rawText,
            Category = string.IsNullOrWhiteSpace(type) ? "ERROR" : type,
            Summary = string.IsNullOrWhiteSpace(firstErrorLine) ? "(오류 메시지 없음)" : firstErrorLine,
            Timestamp = timestamp,
            PlainDetail = detail.ToString(),
        };
    }

    // ===================================================================
    // 3) EQS_Trace_*.log : "클래스,메서드, ..., 시간, 예외" 헤더 + 스택 트레이스
    // ===================================================================

    private static LogParseResult ParseEqsTrace(string[] lines)
    {
        var entries = new List<LogEntry>();
        var currentBlock = new List<string>();
        var blockStartLine = 0;

        void FlushBlock()
        {
            if (currentBlock.Count == 0)
            {
                return;
            }

            entries.Add(BuildEqsTraceEntry(blockStartLine, currentBlock));
            currentBlock = [];
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (EqsTraceHeaderPattern().IsMatch(line))
            {
                // 새 항목의 헤더 줄을 만나면, 지금까지 모은 줄(이전 항목의 스택 트레이스)을 확정한다.
                FlushBlock();
                blockStartLine = i + 1;
            }

            currentBlock.Add(line);
        }

        FlushBlock();

        return new LogParseResult(entries, 0, lines.Length);
    }

    private static LogEntry BuildEqsTraceEntry(int startLineNumber, List<string> blockLines)
    {
        var headerMatch = EqsTraceHeaderPattern().Match(blockLines[0]);
        var rawText = string.Join(Environment.NewLine, blockLines);

        if (!headerMatch.Success)
        {
            return new LogEntry
            {
                LineNumber = startLineNumber,
                RawText = rawText,
                Category = "EQS_TRACE",
                Summary = blockLines[0],
                PlainDetail = rawText,
            };
        }

        var chainText = headerMatch.Groups["chain"].Value.Trim().TrimEnd(',');
        var exception = headerMatch.Groups["exception"].Value.Trim();
        var timestampText = headerMatch.Groups["timestamp"].Value.Trim();

        // "yyyy-MM-dd 오후 h:mm:ss" 형식의 한국어 오전/오후 표기는 ko-KR 문화권으로만 정확히 파싱된다.
        DateTimeOffset? timestamp = DateTime.TryParseExact(
            timestampText,
            "yyyy-MM-dd tt h:mm:ss",
            CultureInfo.GetCultureInfo("ko-KR"),
            DateTimeStyles.None,
            out var parsed)
            ? new DateTimeOffset(parsed, TimeZoneInfo.Local.GetUtcOffset(parsed))
            : null;

        var chainParts = chainText
            .Split(',')
            .Select(part => part.Trim())
            .Where(part => part.Length > 0)
            .ToList();

        // 콤마로 한 줄에 뭉쳐 있던 호출 체인을 항목별로 줄바꿈하고,
        // 예외 메시지 / 스택 트레이스를 구획을 나눠 보여준다.
        var detail = new StringBuilder();
        detail.AppendLine($"시간 : {timestampText}");
        detail.AppendLine();
        detail.AppendLine("■ 호출 체인");
        foreach (var part in chainParts)
        {
            detail.AppendLine($"  - {part}");
        }
        detail.AppendLine();
        detail.AppendLine("■ 예외");
        detail.AppendLine(exception);
        if (blockLines.Count > 1)
        {
            detail.AppendLine();
            detail.AppendLine("■ 스택 트레이스");
            for (var i = 1; i < blockLines.Count; i++)
            {
                detail.AppendLine(blockLines[i]);
            }
        }

        return new LogEntry
        {
            LineNumber = startLineNumber,
            RawText = rawText,
            Category = "EQS_TRACE",
            Summary = exception,
            Timestamp = timestamp,
            PlainDetail = detail.ToString(),
        };
    }
}
