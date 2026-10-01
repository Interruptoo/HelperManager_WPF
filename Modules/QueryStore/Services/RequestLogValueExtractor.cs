using System.Text.Json;
using HelperManager.Modules.QueryStore.Models;

namespace HelperManager.Modules.QueryStore.Services;

/// <summary>
/// RequestInfo(요청 로그 뷰어) 화면에서 복사한 "■ 필드명 { JSON... }" 형태의 텍스트에서
/// 평탄화된 key-value 쌍을 뽑아내는 도우미입니다.
/// 이렇게 뽑은 값을 Query Store 의 파라미터 이름과 매칭해서, 로그에서 실제로 쓰인 요청 값을
/// 그대로 파라미터에 채워 넣을 수 있게 해준다(DB 연결 없이 수동으로 값을 입력하는 수고를 던다).
/// </summary>
public static class RequestLogValueExtractor
{
    /// <summary>
    /// 붙여넣은 텍스트 안에서 중첩/문자열 내부를 고려해 모든 JSON 객체({...}) 블록을 찾아 파싱하고,
    /// 모든 블록의 속성을 하나로 합친 key -> (값, 추정 타입) 사전을 반환한다.
    /// (RequestInfo 화면은 RequestContent/ResponseContent 등 여러 "■ 필드명" 블록을 함께 보여줄 수
    ///  있으므로, 특정 필드명에 의존하지 않고 텍스트 전체에서 JSON처럼 보이는 블록을 모두 찾는다.)
    /// 같은 이름이 여러 블록에 있으면 나중에 나오는 블록의 값으로 덮어쓴다.
    /// JSON으로 파싱되지 않는 블록(또는 JSON이 아닌 나머지 텍스트)은 조용히 무시한다.
    /// </summary>
    public static IReadOnlyDictionary<string, (string Value, ParameterValueType Type)> ExtractFlatValues(string pastedText)
    {
        var result = new Dictionary<string, (string Value, ParameterValueType Type)>(StringComparer.OrdinalIgnoreCase);

        foreach (var block in ExtractJsonObjectBlocks(pastedText))
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(block);
            }
            catch (JsonException)
            {
                continue; // JSON이 아니거나 깨진 블록은 건너뛴다.
            }

            using (document)
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var property in document.RootElement.EnumerateObject())
                {
                    result[property.Name] = ConvertJsonValue(property.Value);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// JSON 값의 종류에 따라 치환용 텍스트와 추정 데이터 타입을 결정한다.
    /// 문자열/숫자만 명확하게 구분하고, 그 외(참/거짓/null/객체/배열)는 타입을 확신할 수 없으므로
    /// 전부 Text로 취급한다. (요청하신 "타입이 불확실하면 Text로" 규칙)
    /// </summary>
    private static (string Value, ParameterValueType Type) ConvertJsonValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => (element.GetString() ?? string.Empty, ParameterValueType.Text),
        JsonValueKind.Number => (element.GetRawText(), ParameterValueType.Number),
        JsonValueKind.Null => (string.Empty, ParameterValueType.Text),
        _ => (element.GetRawText(), ParameterValueType.Text), // true/false, 객체, 배열 등
    };

    /// <summary>
    /// 문자열 리터럴 내부의 중괄호는 무시하면서, 중첩된 중괄호 깊이를 추적해
    /// 텍스트 안에 있는 최상위 JSON 객체({...}) 블록들을 전부 찾아낸다.
    /// </summary>
    private static List<string> ExtractJsonObjectBlocks(string text)
    {
        var blocks = new List<string>();
        var depth = 0;
        var insideString = false;
        var startIndex = -1;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (insideString)
            {
                if (c == '\\')
                {
                    i++; // 이스케이프된 다음 문자(예: \" 안의 ")는 그대로 건너뛴다.
                }
                else if (c == '"')
                {
                    insideString = false;
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    insideString = true;
                    break;

                case '{':
                    if (depth == 0)
                    {
                        startIndex = i;
                    }

                    depth++;
                    break;

                case '}':
                    if (depth > 0)
                    {
                        depth--;
                        if (depth == 0 && startIndex >= 0)
                        {
                            blocks.Add(text[startIndex..(i + 1)]);
                            startIndex = -1;
                        }
                    }

                    break;
            }
        }

        return blocks;
    }
}
