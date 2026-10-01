using System.Text.Json;
using System.Text.Json.Nodes;

namespace HelperManager.Common;

/// <summary>
/// JSON 값을 사람이 읽기 좋은 형태(들여쓰기)의 문자열로 변환하는 유틸리티입니다.
///
/// 이 프로젝트가 다루는 로그 파일(chisservice_inout_*.txt / *.clog)은 한 줄짜리 JSON 안에
/// RequestHeader / RequestContent / ResponseContent 같은 필드가 "다시 한 번 JSON 문자열로
/// 이스케이프된" 형태로 들어있습니다. (예: "RequestContent":"{\"a\":{...}}")
/// 그래서 단순히 최상위 JSON만 들여쓰기 하면 정작 중요한 body 내용은 여전히 한 줄로 뭉쳐서
/// 보이게 됩니다. 이를 해결하기 위해 문자열 값이 다시 JSON으로 파싱 가능한지 검사하고,
/// 가능하면 한 번 더 파싱해서 들여쓰기를 적용합니다.
/// </summary>
public static class JsonPrettyPrinter
{
    private static readonly JsonSerializerOptions IndentedOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// JsonNode 하나를 가독성 좋은 문자열로 변환한다.
    /// 값이 "JSON을 문자열로 담고 있는 경우"(중첩 JSON)라면 재귀적으로 한 번 더 예쁘게 출력한다.
    /// </summary>
    public static string PrettyPrint(JsonNode? node)
    {
        if (node is null)
        {
            return "null";
        }

        // 값이 순수 문자열(JsonValue)인 경우에만 "중첩 JSON일 가능성"을 검사한다.
        if (node is JsonValue value && value.TryGetValue(out string? textValue) && !string.IsNullOrWhiteSpace(textValue))
        {
            var nestedJson = TryParseNestedJson(textValue);
            if (nestedJson is not null)
            {
                // 문자열 안에 JSON이 들어있었다면, 그 내용을 다시 들여쓰기해서 반환한다.
                return nestedJson.ToJsonString(IndentedOptions);
            }

            // JSON이 아닌 일반 문자열이면 원본 그대로 반환한다. (예: 순수 텍스트 값)
            return textValue;
        }

        // 객체/배열/숫자/불리언 등은 System.Text.Json 의 기본 들여쓰기 기능을 사용한다.
        return node.ToJsonString(IndentedOptions);
    }

    /// <summary>
    /// 문자열이 JSON으로 파싱 가능하면 파싱 결과를 반환하고, 아니면 null 을 반환한다.
    /// (로그 안의 일반 텍스트 값까지 JSON으로 오해하지 않도록 예외를 안전하게 흡수한다.)
    /// </summary>
    private static JsonNode? TryParseNestedJson(string text)
    {
        var trimmed = text.TrimStart();

        // JSON 객체({...}) 또는 배열([...])로 시작하는 경우에만 파싱을 시도해서
        // 불필요한 예외 발생(성능 저하)을 줄인다.
        if (trimmed.Length == 0 || (trimmed[0] != '{' && trimmed[0] != '['))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
