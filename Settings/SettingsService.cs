using System.IO;
using System.Text.Json;

namespace HelperManager.Settings;

/// <summary>
/// <see cref="ISettingsService"/> 의 실제 구현체입니다.
/// 설정 파일은 앱이 실제로 실행되는 폴더(<see cref="AppContext.BaseDirectory"/>)의
/// settings.json 에 저장한다. (.config/App.config 같은 구식 WPF 설정 방식 대신 JSON을 쓰는 이유:
/// 최신 .NET 에서는 ConfigurationManager 없이 System.Text.Json 만으로 바로 읽고 쓸 수 있고,
/// 사람이 직접 열어봐도 이해하기 쉬우며, 리스트/중첩 구조 같은 값도 자연스럽게 표현되기 때문이다.)
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _filePath;

    public SettingsService()
    {
        _filePath = Path.Combine(AppContext.BaseDirectory, "settings.json");
        Current = Load();
    }

    public AppSettings Current { get; private set; }

    public string FilePath => _filePath;

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        File.WriteAllText(_filePath, json);
        Current = settings;
    }

    private AppSettings Load()
    {
        if (!File.Exists(_filePath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (JsonException)
        {
            // 설정 파일이 손상되어 있으면, 앱이 뜨지 못하는 것보다는 기본값으로 시작하는 편이 낫다.
            return new AppSettings();
        }
    }
}
