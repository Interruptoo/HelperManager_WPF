using System.IO;
using HelperManager.Modules.ComnCode.Models;
using HelperManager.Settings;

namespace HelperManager.Modules.ComnCode.Services;

/// <summary><see cref="IComnCodeDataProvider"/> 의 구현체입니다.</summary>
public sealed class ComnCodeDataProvider : IComnCodeDataProvider
{
    private readonly IComnCodeParserService _parserService;
    private readonly ISettingsService _settingsService;

    // null 이면 "아직 안 읽었거나 버려진 상태"다. 빈 목록([])은 "읽었는데 내용이 없다"와 구분된다.
    private IReadOnlyList<ComnCodeRecord>? _groups;
    private IReadOnlyList<ComnCodeRecord>? _details;

    public ComnCodeDataProvider(IComnCodeParserService parserService, ISettingsService settingsService)
    {
        _parserService = parserService;
        _settingsService = settingsService;
    }

    public IReadOnlyList<ComnCodeRecord> Groups =>
        _groups ??= Read(_settingsService.Current.ComnCdInfoPath);

    public IReadOnlyList<ComnCodeRecord> Details =>
        _details ??= Read(_settingsService.Current.ComnCdDetailPath);

    public void Invalidate()
    {
        _groups = null;
        _details = null;
    }

    private IReadOnlyList<ComnCodeRecord> Read(string? filePath) =>
        !string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath)
            ? _parserService.ParseFile(filePath)
            : [];
}
