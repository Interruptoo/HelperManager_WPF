using System.Text;
using System.Text.Json;

namespace HelperManager.Extractor;

/// <summary>QUERIES.json 의 항목 하나 — "이 쿼리를 돌려서 이 파일로 저장한다".</summary>
public sealed record QueryEntry(string FileName, string Section, string Sql);

/// <summary>
/// QUERIES.json(쿼리 모음 매니페스트)을 읽습니다.
///
/// 파일 모양:
///   { "DESCRIPTION": ..., "SOURCE_FILE": ..., "NOTE": ..., "QUERIES": [ {FILE_NAME, SQL_TEXT, ...}, ... ] }
/// </summary>
public static class QueryManifest
{
    public static IReadOnlyList<QueryEntry> Load(string path)
    {
        var json = File.ReadAllText(path, Encoding.UTF8);
        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("QUERIES", out var queries) ||
            queries.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"QUERIES 배열을 찾지 못했습니다: {path}");
        }

        var entries = new List<QueryEntry>();
        foreach (var item in queries.EnumerateArray())
        {
            var fileName = GetString(item, "FILE_NAME");
            var sql = GetString(item, "SQL_TEXT");

            // 파일명이나 쿼리가 비어 있는 항목은 추출할 것이 없으므로 조용히 건너뛴다.
            if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(sql))
            {
                continue;
            }

            entries.Add(new QueryEntry(fileName, GetString(item, "SECTION") ?? string.Empty, sql));
        }

        return entries;
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
