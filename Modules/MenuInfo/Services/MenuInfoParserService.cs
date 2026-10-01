using System.IO;
using System.Text;
using System.Text.Json;
using HelperManager.Modules.MenuInfo.Models;

namespace HelperManager.Modules.MenuInfo.Services;

/// <summary><see cref="IMenuInfoParserService"/> 의 실제 구현체입니다.</summary>
public sealed class MenuInfoParserService : IMenuInfoParserService
{
    public IReadOnlyList<MenuInfoEntry> ParseFile(string filePath)
    {
        var json = File.ReadAllText(filePath, Encoding.UTF8);
        using var document = JsonDocument.Parse(json);

        var array = FindEntriesArray(document.RootElement);
        if (array is null)
        {
            return [];
        }

        var entries = new List<MenuInfoEntry>();
        foreach (var item in array.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            entries.Add(new MenuInfoEntry(
                UseYn: GetString(item, "USE_YN"),
                DispYn: GetString(item, "DISP_YN"),
                MenuNm: GetString(item, "MENU_NM"),
                MenuCd: GetString(item, "MENU_CD"),
                AssemblyNm: GetString(item, "ASSEMBLY_NM"),
                AppUrl: GetString(item, "APP_URL"),
                Depth1: GetString(item, "DEPTH1"),
                Depth2: GetString(item, "DEPTH2")));
        }

        return entries;
    }

    /// <summary>
    /// 최상위가 배열이면 그대로, 객체면 그 안에서 배열 값을 가진 첫 속성을 찾아 반환한다.
    /// (내보내기 도구에 따라 "[...]" 그대로일 수도, "{"items": [...]}" 처럼 감싸져 있을 수도 있다.)
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
    /// 수 있도록 적당히 텍스트로 변환하고, 속성이 없거나 null 이면 null 을 반환한다.
    /// </summary>
    private static string? GetString(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => value.GetRawText(),
        };
    }
}
