using System.Data;
using System.Text.Encodings.Web;
using System.Text.Json;
using Oracle.ManagedDataAccess.Client;

namespace HelperManager.Extractor;

/// <summary>
/// 쿼리 결과를 각 화면이 읽는 형태의 JSON 배열로 써 내보냅니다.
///
/// 출력 모양은 기존에 손으로 뽑아 쓰던 파일과 같은 "레코드 배열"이다:
///   [ { "OWNER": "HSUP", "TABLE_NAME": "...", "CREATED_DAYS": 0 }, ... ]
///
/// ComnCdDetail 처럼 70MB 가 넘는 결과도 있어서, 전체를 메모리에 담지 않고
/// 읽는 즉시 파일로 흘려보낸다(스트리밍).
/// </summary>
public static class JsonResultWriter
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        // 한글이 \uXXXX 로 깨져 보이지 않도록 그대로 쓴다. (기존 추출 파일과 동일)
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>쿼리를 실행해 결과를 <paramref name="filePath"/> 에 쓰고, 기록한 행 수를 돌려준다.</summary>
    public static long Write(OracleConnection connection, string sql, string filePath, int commandTimeoutSeconds)
    {
        // 다 쓰기 전에 죽어도 기존 파일이 반쪽짜리로 덮이지 않도록, 임시 파일에 쓰고 마지막에 바꾼다.
        var tempPath = filePath + ".tmp";

        using var command = new OracleCommand(sql, connection)
        {
            CommandTimeout = commandTimeoutSeconds,
            // 한 번에 가져오는 양을 늘려 왕복 횟수를 줄인다. (수십만 행 추출에서 체감 차이가 크다)
            FetchSize = 1024 * 1024,
        };

        long rows;
        using (var stream = File.Create(tempPath))
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        using (var reader = command.ExecuteReader())
        {
            var columnNames = new string[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
            {
                columnNames[i] = reader.GetName(i);
            }

            writer.WriteStartArray();
            rows = 0;

            while (reader.Read())
            {
                writer.WriteStartObject();
                for (var i = 0; i < columnNames.Length; i++)
                {
                    writer.WritePropertyName(columnNames[i]);
                    WriteValue(writer, reader, i);
                }

                writer.WriteEndObject();
                rows++;
            }

            writer.WriteEndArray();
        }

        File.Move(tempPath, filePath, overwrite: true);
        return rows;
    }

    /// <summary>
    /// 컬럼 값 하나를 JSON 으로 쓴다. 숫자는 숫자로, 날짜는 사람이 읽을 수 있는 문자열로,
    /// NULL 은 null 로 쓴다. (기존 추출 파일도 CREATED_DAYS 같은 값이 따옴표 없는 숫자였다)
    /// </summary>
    private static void WriteValue(Utf8JsonWriter writer, IDataRecord reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            writer.WriteNullValue();
            return;
        }

        var value = reader.GetValue(ordinal);
        switch (value)
        {
            case string text:
                writer.WriteStringValue(text);
                break;
            case DateTime date:
                writer.WriteStringValue(date.ToString("yyyy-MM-dd HH:mm:ss"));
                break;
            case decimal number:
                writer.WriteNumberValue(number);
                break;
            case short or int or long or byte or sbyte or ushort or uint:
                writer.WriteNumberValue(Convert.ToInt64(value));
                break;
            case float or double:
                writer.WriteNumberValue(Convert.ToDouble(value));
                break;
            case bool flag:
                writer.WriteBooleanValue(flag);
                break;
            default:
                // CLOB/RAW 등 그 밖의 타입은 문자열로 떨어뜨린다.
                writer.WriteStringValue(value.ToString());
                break;
        }
    }
}
