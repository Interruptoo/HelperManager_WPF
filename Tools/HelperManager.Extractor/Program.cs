using System.Diagnostics;
using System.Text;
using System.Text.Json;
using HelperManager.Extractor;
using HelperManager.Settings;
using Oracle.ManagedDataAccess.Client;

// ============================================================================
//  HelperManager.Extractor
//
//  각 화면이 읽는 JSON 파일(TableInfo.json, ColumnInfo.json, ComnCdDetail.json ...)을
//  DB 에서 직접 뽑아오는 작은 콘솔 프로그램입니다.
//
//  본체(HelperManager.exe)는 DB 에 붙지 않는다는 처음 기획을 지키기 위해, DB 접속은 전부
//  이 exe 안에만 있다. Settings 화면의 [지금 추출] 버튼이 이 exe 를 실행하고, 작업 스케줄러나
//  extract.bat 으로 따로 돌릴 수도 있다.
//
//  사용법:
//    HelperManager.Extractor.exe [옵션]
//      --settings <경로>   settings.json 경로 (기본: exe 와 같은 폴더, 없으면 상위 폴더)
//      --queries  <경로>   QUERIES.json 경로 (기본: settings.json 의 Extract.QueriesJsonPath)
//      --out      <폴더>   출력 폴더        (기본: settings.json 의 Extract.OutputFolder)
//      --only     <파일명> 특정 항목만 추출 (예: --only TableInfo.json, 여러 번 지정 가능)
//      --list              무엇을 추출할지 목록만 보여주고 끝낸다 (DB 접속 안 함)
//
//  종료 코드: 0 = 전부 성공, 1 = 하나라도 실패, 2 = 설정/인자 문제
// ============================================================================

Console.OutputEncoding = Encoding.UTF8;

try
{
    return Run(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[오류] {ex.Message}");
    return 2;
}

static int Run(string[] args)
{
    var options = CommandLineOptions.Parse(args);

    var settingsPath = options.SettingsPath ?? FindSettingsFile();
    if (settingsPath is null)
    {
        Console.Error.WriteLine("[오류] settings.json 을 찾지 못했습니다. --settings 로 경로를 지정해 주세요.");
        return 2;
    }

    var settings = LoadSettings(settingsPath);
    var extract = settings.Extract;

    var queriesPath = options.QueriesPath ?? extract.QueriesJsonPath;
    if (string.IsNullOrWhiteSpace(queriesPath) || !File.Exists(queriesPath))
    {
        Console.Error.WriteLine($"[오류] QUERIES.json 을 찾지 못했습니다: {queriesPath ?? "(설정 없음)"}");
        return 2;
    }

    // 출력 폴더를 따로 정하지 않았으면 QUERIES.json 이 있는 폴더에 쓴다.
    var outputFolder = options.OutputFolder
        ?? (string.IsNullOrWhiteSpace(extract.OutputFolder) ? Path.GetDirectoryName(Path.GetFullPath(queriesPath))! : extract.OutputFolder);

    var entries = QueryManifest.Load(queriesPath);
    if (options.Only.Count > 0)
    {
        entries = entries.Where(e => options.Only.Contains(e.FileName, StringComparer.OrdinalIgnoreCase)).ToList();
    }

    Console.WriteLine($"설정   : {settingsPath}");
    Console.WriteLine($"쿼리   : {queriesPath}");
    Console.WriteLine($"출력   : {outputFolder}");
    Console.WriteLine($"대상   : {entries.Count}건");
    Console.WriteLine();

    if (entries.Count == 0)
    {
        Console.WriteLine("추출할 항목이 없습니다.");
        return 0;
    }

    if (options.ListOnly)
    {
        foreach (var entry in entries)
        {
            Console.WriteLine($"  [{entry.Section}] {entry.FileName}");
        }

        return 0;
    }

    var connectionString = BuildConnectionString(extract);
    if (connectionString is null)
    {
        Console.Error.WriteLine("[오류] DB 접속 정보가 비어 있습니다. Settings 화면에서 Host / ServiceName / UserId / Password 를 채워 주세요.");
        return 2;
    }

    Directory.CreateDirectory(outputFolder);

    using var connection = new OracleConnection(connectionString);
    Console.Write($"접속 중 {extract.UserId}@{extract.Host}:{extract.Port}/{extract.ServiceName} ... ");
    connection.Open();
    Console.WriteLine("성공");
    Console.WriteLine();

    var failed = 0;
    var totalWatch = Stopwatch.StartNew();

    foreach (var entry in entries)
    {
        var target = Path.Combine(outputFolder, entry.FileName);
        Console.Write($"  {entry.FileName,-26} ... ");

        var watch = Stopwatch.StartNew();
        try
        {
            var rows = JsonResultWriter.Write(connection, entry.Sql, target, extract.CommandTimeoutSeconds);
            watch.Stop();

            var size = new FileInfo(target).Length;
            Console.WriteLine($"{rows,9:N0} 행  {FormatSize(size),9}  {watch.Elapsed.TotalSeconds,6:N1}s");
        }
        catch (Exception ex)
        {
            watch.Stop();
            failed++;
            Console.WriteLine("실패");
            Console.Error.WriteLine($"      -> {ex.Message.Trim()}");
        }
    }

    totalWatch.Stop();
    Console.WriteLine();
    Console.WriteLine($"완료: {entries.Count - failed}/{entries.Count} 성공, 총 {totalWatch.Elapsed.TotalSeconds:N1}초");

    return failed > 0 ? 1 : 0;
}

/// <summary>
/// exe 와 같은 폴더의 settings.json 을 먼저 보고, 없으면 상위 폴더를 본다.
/// (추출기를 본체 출력 폴더의 Extractor 하위 폴더에 두기 때문에, 본체의 settings.json 은 한 단계 위에 있다)
/// </summary>
static string? FindSettingsFile()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);

    for (var i = 0; i < 3 && directory is not null; i++, directory = directory.Parent)
    {
        var candidate = Path.Combine(directory.FullName, "settings.json");
        if (File.Exists(candidate))
        {
            return candidate;
        }
    }

    return null;
}

static AppSettings LoadSettings(string path)
{
    var json = File.ReadAllText(path, Encoding.UTF8);
    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
}

/// <summary>설정값으로 Oracle 접속 문자열을 만든다. 필수 항목이 비어 있으면 null.</summary>
static string? BuildConnectionString(ExtractSettings extract)
{
    if (string.IsNullOrWhiteSpace(extract.Host) ||
        string.IsNullOrWhiteSpace(extract.ServiceName) ||
        string.IsNullOrWhiteSpace(extract.UserId))
    {
        return null;
    }

    var builder = new OracleConnectionStringBuilder
    {
        DataSource = $"{extract.Host}:{extract.Port}/{extract.ServiceName}",
        UserID = extract.UserId,
        Password = extract.Password ?? string.Empty,
        // 추출은 쿼리 몇 개를 연달아 돌리는 단발성 작업이라 커넥션 풀이 필요 없다.
        Pooling = false,
    };

    return builder.ConnectionString;
}

static string FormatSize(long bytes) => bytes switch
{
    >= 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024 / 1024:N1} GB",
    >= 1024 * 1024 => $"{bytes / 1024.0 / 1024:N1} MB",
    >= 1024 => $"{bytes / 1024.0:N1} KB",
    _ => $"{bytes} B",
};

/// <summary>명령줄 인자를 담는 그릇.</summary>
file sealed class CommandLineOptions
{
    public string? SettingsPath { get; private init; }

    public string? QueriesPath { get; private init; }

    public string? OutputFolder { get; private init; }

    public HashSet<string> Only { get; private init; } = new(StringComparer.OrdinalIgnoreCase);

    public bool ListOnly { get; private init; }

    public static CommandLineOptions Parse(string[] args)
    {
        string? settings = null;
        string? queries = null;
        string? output = null;
        var only = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var listOnly = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--settings":
                    settings = NextValue(args, ref i);
                    break;
                case "--queries":
                    queries = NextValue(args, ref i);
                    break;
                case "--out":
                    output = NextValue(args, ref i);
                    break;
                case "--only":
                    only.Add(NextValue(args, ref i));
                    break;
                case "--list":
                    listOnly = true;
                    break;
                default:
                    throw new ArgumentException($"알 수 없는 인자입니다: {args[i]}");
            }
        }

        return new CommandLineOptions
        {
            SettingsPath = settings,
            QueriesPath = queries,
            OutputFolder = output,
            Only = only,
            ListOnly = listOnly,
        };
    }

    private static string NextValue(string[] args, ref int index)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{args[index]} 뒤에 값이 필요합니다.");
        }

        return args[++index];
    }
}
