using System.IO;
using System.Text;
using System.Text.Json;

namespace HelperManager.Common;

/// <summary>
/// 내보내기 도구마다 다른 JSON 형태 — 순수 배열("[...]") 이거나, 배열을 속성으로 감싼 객체
/// ("{"items": [...]}") — 에서 레코드 배열을 찾아 각 객체를 key-value 사전으로 바꿔주는 공용 파서입니다.
/// MenuInfo, ComnCode 처럼 "정기적으로 추출되는 JSON 레코드 목록을 읽어와 화면에 보여주는" 여러
/// 화면에서 공통으로 사용한다. 필드 구성(스키마)을 미리 알 수 없는 경우를 위해, 고정된 모델이 아니라
/// 원본 그대로의 key-value 사전으로 돌려준다.
/// </summary>
public static class FlexibleJsonArrayParser
{
    /// <summary>
    /// JSON 파일을 읽어 레코드 목록(사전의 목록)으로 변환한다. 값이 문자열이 아니어도(숫자/불리언 등)
    /// 화면에 표시할 수 있도록 적당히 텍스트로 바꾸고, 값이 null 이면 null 을 넣는다.
    /// 각 사전은 필드명을 대소문자 구분 없이 찾을 수 있도록 생성된다.
    /// </summary>
    public static IReadOnlyList<Dictionary<string, string?>> ParseFile(string filePath)
    {
        var json = File.ReadAllText(filePath, Encoding.UTF8);
        json = SanitizeControlCharacters(json);
        using var document = JsonDocument.Parse(json);

        var array = FindEntriesArray(document.RootElement);
        if (array is null)
        {
            return [];
        }

        var records = new List<Dictionary<string, string?>>();
        foreach (var item in array.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in item.EnumerateObject())
            {
                fields[property.Name] = ConvertValue(property.Value);
            }

            records.Add(fields);
        }

        return records;
    }

    /// <summary>
    /// 최상위가 배열이면 그대로, 객체면 그 안에서 배열 값을 가진 첫 속성을 찾아 반환한다.
    /// </summary>
    private static JsonElement? FindEntriesArray(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root;
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in root.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    return property.Value;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 속성 값을 문자열로 안전하게 꺼낸다. 원본이 문자열이 아니어도(숫자/불리언 등) 화면에 표시할
    /// 수 있도록 적당히 텍스트로 변환하고, null 이면 null 을 반환한다.
    /// </summary>
    private static string? ConvertValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => null,
        _ => value.GetRawText(),
    };

    /// <summary>
    /// 일부 DB 추출 도구가 내보낸 파일에는 문자열 값 안에 이스케이프되지 않은 제어 문자(예: 0x07 BEL
    /// 같은 레거시 데이터 잔재)가 그대로 섞여 있어, JSON 사양을 엄격히 따르는 System.Text.Json 이
    /// 파싱을 거부하는 경우가 있다. 이 화면들은 데이터를 "보여주기"만 하면 되므로, 제어 문자(tab/개행
    /// 포함)를 전부 공백으로 바꿔서라도 파싱이 되게 한다 — 구조적으로 쓰인 공백 문자를 다른 공백
    /// 문자로 바꾸는 것뿐이라 JSON 구조 자체는 전혀 영향받지 않는다.
    /// </summary>
    private static string SanitizeControlCharacters(string json)
    {
        StringBuilder? builder = null;

        for (var i = 0; i < json.Length; i++)
        {
            if (json[i] >= 0x20)
            {
                builder?.Append(json[i]);
                continue;
            }

            // 제어 문자를 처음 만난 시점에만 StringBuilder 를 만들어서, 제어 문자가 없는
            // (대부분의) 파일에서는 불필요한 전체 복사를 하지 않는다.
            if (builder is null)
            {
                builder = new StringBuilder(json.Length);
                builder.Append(json, 0, i);
            }

            builder.Append(' ');
        }

        return builder?.ToString() ?? json;
    }
}
