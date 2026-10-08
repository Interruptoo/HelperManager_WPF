using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelperManager.Common;
using HelperManager.Modules;
using HelperManager.Settings;
using Microsoft.Win32;

namespace HelperManager.Modules.Settings;

/// <summary>
/// Settings 화면의 ViewModel.
/// 매번 폴더/파일을 다시 찾는 수고를 덜기 위해, 자주 쓰는 경로를 미리 등록해두는 화면이다.
///   - Request LogViewer : 앱을 시작할 때 탭으로 자동으로 열어줄 로그 폴더 목록 (여러 개 가능)
///   - Query Store        : 앱을 시작할 때 자동으로 읽어줄 쿼리 모음 XML 파일 하나
///   - MenuInfo            : 앱을 시작할 때 자동으로 읽어줄 메뉴 정보 JSON 파일 하나
///   - Common Code         : 앱을 시작할 때 자동으로 읽어줄 ComnCdInfo.json / ComnCdDetail.json 파일
///   - Table Info          : 앱을 시작할 때 자동으로 읽어줄 테이블/컬럼/인덱스/사용오브젝트 JSON 파일
/// [저장]을 누르면 JSON 설정 파일에 기록되고, 다음 실행부터 반영된다.
///
/// 여기에 더해 "JSON 추출" 영역이 있다. 위 화면들이 읽는 JSON 파일들은 원래 사람이 주기적으로
/// DB 에서 직접 뽑아 와야 했는데, 그 작업을 [지금 추출] 버튼 하나로 할 수 있게 한 것이다.
/// 본체는 DB 에 붙지 않는다는 처음 기획을 지키기 위해, 실제 DB 접속은 별도 프로그램
/// (Extractor\HelperManager.Extractor.exe)이 하고 이 화면은 그 프로그램을 띄워 출력만 받아 보여준다.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;

    // 추출이 끝나면 각 화면이 JSON 을 다시 읽도록 알려주는 통로.
    private readonly IJsonDataRefreshNotifier _refreshNotifier;

    public SettingsViewModel(
        ISettingsService settingsService,
        IEnumerable<IFeatureModule> modules,
        IJsonDataRefreshNotifier refreshNotifier)
    {
        _settingsService = settingsService;
        _refreshNotifier = refreshNotifier;

        // 기능키 콤보박스에 띄울 목록: 맨 앞에 "지정 안 함", 그 뒤로 등록된 메뉴 전부.
        AvailableMenuTitles = new[] { FunctionKeyBinding.NoneLabel }
            .Concat(modules.Select(module => module.Title))
            .ToList();

        // F1~F12 는 메뉴 개수와 무관하게 항상 12줄을 보여준다. (지금 없는 메뉴도 나중에 생기면 바로 지정 가능)
        var savedKeyMenus = settingsService.Current.FunctionKeyMenus;
        FunctionKeyBindings = new ObservableCollection<FunctionKeyBinding>(
            Enumerable.Range(1, 12).Select(number =>
            {
                var keyName = $"F{number}";
                var savedTitle = savedKeyMenus.GetValueOrDefault(keyName);

                return new FunctionKeyBinding
                {
                    KeyName = keyName,

                    // 저장된 메뉴 이름이 지금 목록에 없으면(메뉴 이름이 바뀌었거나 빠진 경우)
                    // 억지로 복원하지 않고 "지정 안 함" 으로 둔다.
                    MenuTitle = savedTitle is not null && AvailableMenuTitles.Contains(savedTitle)
                        ? savedTitle
                        : FunctionKeyBinding.NoneLabel,
                };
            }));

        RequestLogViewerFolderPaths = new ObservableCollection<string>(settingsService.Current.RequestLogViewerFolderPaths);
        queryStoreXmlPath = settingsService.Current.QueryStoreXmlPath;
        menuInfoJsonPath = settingsService.Current.MenuInfoJsonPath;
        comnCdInfoPath = settingsService.Current.ComnCdInfoPath;
        comnCdDetailPath = settingsService.Current.ComnCdDetailPath;
        tableInfoJsonPath = settingsService.Current.TableInfoJsonPath;
        columnInfoJsonPath = settingsService.Current.ColumnInfoJsonPath;
        indexInfoJsonPath = settingsService.Current.IndexInfoJsonPath;
        tableUseObjectJsonPath = settingsService.Current.TableUseObjectJsonPath;

        var extract = settingsService.Current.Extract;
        extractHost = extract.Host;
        extractPort = extract.Port;
        extractServiceName = extract.ServiceName;
        extractUserId = extract.UserId;
        extractPassword = extract.Password;
        extractQueriesJsonPath = extract.QueriesJsonPath;
        extractOutputFolder = extract.OutputFolder;

        AddLogFolderCommand = new RelayCommand(AddLogFolder);
        RemoveLogFolderCommand = new RelayCommand<string>(RemoveLogFolder);
        BrowseQueryStoreXmlCommand = new RelayCommand(BrowseQueryStoreXml);
        ClearQueryStoreXmlCommand = new RelayCommand(() => QueryStoreXmlPath = null);
        BrowseMenuInfoJsonCommand = new RelayCommand(BrowseMenuInfoJson);
        ClearMenuInfoJsonCommand = new RelayCommand(() => MenuInfoJsonPath = null);
        BrowseComnCdInfoCommand = new RelayCommand(BrowseComnCdInfo);
        ClearComnCdInfoCommand = new RelayCommand(() => ComnCdInfoPath = null);
        BrowseComnCdDetailCommand = new RelayCommand(BrowseComnCdDetail);
        ClearComnCdDetailCommand = new RelayCommand(() => ComnCdDetailPath = null);
        BrowseTableInfoJsonCommand = new RelayCommand(() => BrowseJson("Table Info가 시작 시 자동으로 불러올 테이블 목록 JSON 파일을 선택하세요", path => TableInfoJsonPath = path));
        ClearTableInfoJsonCommand = new RelayCommand(() => TableInfoJsonPath = null);
        BrowseColumnInfoJsonCommand = new RelayCommand(() => BrowseJson("Table Info가 시작 시 자동으로 불러올 컬럼 목록 JSON 파일을 선택하세요", path => ColumnInfoJsonPath = path));
        ClearColumnInfoJsonCommand = new RelayCommand(() => ColumnInfoJsonPath = null);
        BrowseIndexInfoJsonCommand = new RelayCommand(() => BrowseJson("Table Info가 시작 시 자동으로 불러올 인덱스 목록 JSON 파일을 선택하세요", path => IndexInfoJsonPath = path));
        ClearIndexInfoJsonCommand = new RelayCommand(() => IndexInfoJsonPath = null);
        BrowseTableUseObjectJsonCommand = new RelayCommand(() => BrowseJson("Table Info가 시작 시 자동으로 불러올 사용 오브젝트 목록 JSON 파일을 선택하세요", path => TableUseObjectJsonPath = path));
        ClearTableUseObjectJsonCommand = new RelayCommand(() => TableUseObjectJsonPath = null);
        BrowseExtractQueriesJsonCommand = new RelayCommand(() => BrowseJson("추출기가 실행할 쿼리 모음(QUERIES.json) 파일을 선택하세요", path => ExtractQueriesJsonPath = path));
        BrowseExtractOutputFolderCommand = new RelayCommand(BrowseExtractOutputFolder);
        RunExtractCommand = new AsyncRelayCommand(RunExtractAsync, () => !IsExtracting);
        SaveCommand = new RelayCommand(SaveSettings);
    }

    /// <summary>앱 시작 시 탭으로 자동으로 열어줄 로그 폴더 목록.</summary>
    public ObservableCollection<string> RequestLogViewerFolderPaths { get; }

    /// <summary>기능키 콤보박스에 띄울 선택지. 맨 앞은 "지정 안 함", 나머지는 등록된 메뉴 이름.</summary>
    public IReadOnlyList<string> AvailableMenuTitles { get; }

    /// <summary>F1~F12 열두 줄. 각 줄이 "이 키를 누르면 어떤 메뉴로 갈지"를 들고 있다.</summary>
    public ObservableCollection<FunctionKeyBinding> FunctionKeyBindings { get; }

    /// <summary>앱 시작 시 자동으로 읽어줄 쿼리 모음 XML 파일 경로.</summary>
    [ObservableProperty]
    private string? queryStoreXmlPath;

    /// <summary>앱 시작 시 자동으로 읽어줄 메뉴 정보 JSON 파일 경로.</summary>
    [ObservableProperty]
    private string? menuInfoJsonPath;

    /// <summary>앱 시작 시 자동으로 읽어줄 ComnCdInfo.json 파일 경로.</summary>
    [ObservableProperty]
    private string? comnCdInfoPath;

    /// <summary>앱 시작 시 자동으로 읽어줄 ComnCdDetail.json 파일 경로.</summary>
    [ObservableProperty]
    private string? comnCdDetailPath;

    /// <summary>앱 시작 시 자동으로 읽어줄 테이블 목록 JSON 파일 경로.</summary>
    [ObservableProperty]
    private string? tableInfoJsonPath;

    /// <summary>앱 시작 시 자동으로 읽어줄 컬럼 목록 JSON 파일 경로.</summary>
    [ObservableProperty]
    private string? columnInfoJsonPath;

    /// <summary>앱 시작 시 자동으로 읽어줄 인덱스 목록 JSON 파일 경로.</summary>
    [ObservableProperty]
    private string? indexInfoJsonPath;

    /// <summary>앱 시작 시 자동으로 읽어줄 사용 오브젝트 목록 JSON 파일 경로.</summary>
    [ObservableProperty]
    private string? tableUseObjectJsonPath;

    // ----- JSON 추출 (별도 프로그램이 DB 에 접속해 위 JSON 파일들을 만들어 준다) -----

    /// <summary>Oracle 서버 호스트명 또는 IP.</summary>
    [ObservableProperty]
    private string? extractHost;

    /// <summary>Oracle 리스너 포트. 보통 1521.</summary>
    [ObservableProperty]
    private int extractPort = 1521;

    /// <summary>접속할 서비스명(SERVICE_NAME).</summary>
    [ObservableProperty]
    private string? extractServiceName;

    /// <summary>접속 계정.</summary>
    [ObservableProperty]
    private string? extractUserId;

    /// <summary>접속 계정 비밀번호. settings.json 에 평문으로 저장된다.</summary>
    [ObservableProperty]
    private string? extractPassword;

    /// <summary>추출기가 실행할 쿼리 모음(QUERIES.json) 경로.</summary>
    [ObservableProperty]
    private string? extractQueriesJsonPath;

    /// <summary>추출한 JSON 파일을 저장할 폴더. 비워두면 QUERIES.json 이 있는 폴더에 쓴다.</summary>
    [ObservableProperty]
    private string? extractOutputFolder;

    /// <summary>추출기(별도 프로세스)가 출력한 진행 상황. 화면 아래 로그 상자에 그대로 보여준다.</summary>
    [ObservableProperty]
    private string extractLog = string.Empty;

    /// <summary>추출이 진행 중인지. 진행 중에는 [지금 추출] 버튼을 막는다.</summary>
    [ObservableProperty]
    private bool isExtracting;

    /// <summary>저장 결과 등을 알려주는 안내 메시지.</summary>
    [ObservableProperty]
    private string statusMessage = string.Empty;

    public IRelayCommand AddLogFolderCommand { get; }

    public IRelayCommand<string> RemoveLogFolderCommand { get; }

    public IRelayCommand BrowseQueryStoreXmlCommand { get; }

    public IRelayCommand ClearQueryStoreXmlCommand { get; }

    public IRelayCommand BrowseMenuInfoJsonCommand { get; }

    public IRelayCommand ClearMenuInfoJsonCommand { get; }

    public IRelayCommand BrowseComnCdInfoCommand { get; }

    public IRelayCommand ClearComnCdInfoCommand { get; }

    public IRelayCommand BrowseComnCdDetailCommand { get; }

    public IRelayCommand ClearComnCdDetailCommand { get; }

    public IRelayCommand BrowseTableInfoJsonCommand { get; }

    public IRelayCommand ClearTableInfoJsonCommand { get; }

    public IRelayCommand BrowseColumnInfoJsonCommand { get; }

    public IRelayCommand ClearColumnInfoJsonCommand { get; }

    public IRelayCommand BrowseIndexInfoJsonCommand { get; }

    public IRelayCommand ClearIndexInfoJsonCommand { get; }

    public IRelayCommand BrowseTableUseObjectJsonCommand { get; }

    public IRelayCommand ClearTableUseObjectJsonCommand { get; }

    public IRelayCommand BrowseExtractQueriesJsonCommand { get; }

    public IRelayCommand BrowseExtractOutputFolderCommand { get; }

    /// <summary>설정을 저장한 뒤 추출기를 실행한다. 실행 중에도 화면이 멈추지 않도록 비동기 커맨드다.</summary>
    public IAsyncRelayCommand RunExtractCommand { get; }

    public IRelayCommand SaveCommand { get; }

    partial void OnIsExtractingChanged(bool value) => RunExtractCommand.NotifyCanExecuteChanged();

    private void AddLogFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Request LogViewer가 시작 시 자동으로 열 로그 폴더를 선택하세요",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (!RequestLogViewerFolderPaths.Contains(dialog.FolderName, StringComparer.OrdinalIgnoreCase))
        {
            RequestLogViewerFolderPaths.Add(dialog.FolderName);
        }
    }

    private void RemoveLogFolder(string? path)
    {
        if (path is not null)
        {
            RequestLogViewerFolderPaths.Remove(path);
        }
    }

    private void BrowseQueryStoreXml()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Query Store가 시작 시 자동으로 불러올 쿼리 모음 XML 파일을 선택하세요",
            Filter = "XML 파일 (*.xml)|*.xml|모든 파일 (*.*)|*.*",
        };

        if (dialog.ShowDialog() == true)
        {
            QueryStoreXmlPath = dialog.FileName;
        }
    }

    private void BrowseMenuInfoJson()
    {
        var dialog = new OpenFileDialog
        {
            Title = "MenuInfo가 시작 시 자동으로 불러올 메뉴 정보 JSON 파일을 선택하세요",
            Filter = "JSON 파일 (*.json)|*.json|모든 파일 (*.*)|*.*",
        };

        if (dialog.ShowDialog() == true)
        {
            MenuInfoJsonPath = dialog.FileName;
        }
    }

    private void BrowseComnCdInfo()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Common Code가 시작 시 자동으로 불러올 ComnCdInfo.json 파일을 선택하세요",
            Filter = "JSON 파일 (*.json)|*.json|모든 파일 (*.*)|*.*",
        };

        if (dialog.ShowDialog() == true)
        {
            ComnCdInfoPath = dialog.FileName;
        }
    }

    private void BrowseComnCdDetail()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Common Code가 시작 시 자동으로 불러올 ComnCdDetail.json 파일을 선택하세요",
            Filter = "JSON 파일 (*.json)|*.json|모든 파일 (*.*)|*.*",
        };

        if (dialog.ShowDialog() == true)
        {
            ComnCdDetailPath = dialog.FileName;
        }
    }

    /// <summary>
    /// JSON 파일 하나를 고르는 공용 대화상자. Table Info 처럼 등록할 파일이 여러 개인 경우
    /// 같은 코드를 반복하지 않도록, 제목과 "고른 경로를 어디에 넣을지"만 받아서 처리한다.
    /// </summary>
    private static void BrowseJson(string title, Action<string> assignPath)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = "JSON 파일 (*.json)|*.json|모든 파일 (*.*)|*.*",
        };

        if (dialog.ShowDialog() == true)
        {
            assignPath(dialog.FileName);
        }
    }

    private void BrowseExtractOutputFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "추출한 JSON 파일을 저장할 폴더를 선택하세요",
        };

        if (dialog.ShowDialog() == true)
        {
            ExtractOutputFolder = dialog.FolderName;
        }
    }

    /// <summary>
    /// 추출기(Extractor\HelperManager.Extractor.exe)를 띄워 JSON 파일들을 다시 뽑는다.
    ///
    /// 추출기는 접속 정보와 쿼리 경로를 settings.json 에서 읽으므로, 실행 전에 화면의 입력값을
    /// 먼저 저장한다. 출력(진행 상황/오류)은 한 줄씩 받아 <see cref="ExtractLog"/> 에 쌓아 보여준다.
    /// </summary>
    private async Task RunExtractAsync()
    {
        // 추출기가 읽는 것은 "파일에 저장된" 설정이므로, 지금 화면에 입력한 값을 먼저 기록해야 한다.
        SaveSettings();

        var exePath = Path.Combine(AppContext.BaseDirectory, "Extractor", "HelperManager.Extractor.exe");
        if (!File.Exists(exePath))
        {
            ExtractLog = $"추출기를 찾지 못했습니다: {exePath}{Environment.NewLine}" +
                         "솔루션을 다시 빌드하면 Extractor 폴더가 만들어집니다.";
            return;
        }

        IsExtracting = true;
        ExtractLog = string.Empty;
        StatusMessage = "추출 중입니다...";

        try
        {
            var startInfo = new ProcessStartInfo(exePath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            startInfo.ArgumentList.Add("--settings");
            startInfo.ArgumentList.Add(_settingsService.FilePath);

            using var process = new Process { StartInfo = startInfo };
            process.OutputDataReceived += (_, e) => AppendExtractLog(e.Data);
            process.ErrorDataReceived += (_, e) => AppendExtractLog(e.Data);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            // 각 화면에는 [새로고침] 버튼이 없으므로, 추출이 끝나면 여기서 다시 읽으라고 알린다.
            // 일부만 실패했더라도 성공한 파일은 새 내용이므로 어차피 다시 읽어야 한다.
            StatusMessage = "화면 데이터를 다시 불러오는 중...";
            AppendExtractLog("열려 있는 화면의 데이터를 다시 불러옵니다...");
            _refreshNotifier.NotifyRefreshed();

            StatusMessage = process.ExitCode == 0
                ? "추출이 끝났습니다."
                : $"추출 중 실패한 항목이 있습니다. (종료 코드 {process.ExitCode})";
        }
        catch (Exception ex)
        {
            AppendExtractLog($"[오류] {ex.Message}");
            StatusMessage = "추출을 실행하지 못했습니다.";
        }
        finally
        {
            IsExtracting = false;
        }
    }

    /// <summary>추출기의 출력 한 줄을 로그에 덧붙인다. 다른 스레드에서 올라오므로 UI 스레드로 넘긴다.</summary>
    private void AppendExtractLog(string? line)
    {
        if (line is null)
        {
            return;
        }

        Application.Current?.Dispatcher.InvokeAsync(() => ExtractLog += line + Environment.NewLine);
    }

    private void SaveSettings()
    {
        var settings = new AppSettings
        {
            RequestLogViewerFolderPaths = RequestLogViewerFolderPaths.ToList(),
            QueryStoreXmlPath = string.IsNullOrWhiteSpace(QueryStoreXmlPath) ? null : QueryStoreXmlPath,
            MenuInfoJsonPath = string.IsNullOrWhiteSpace(MenuInfoJsonPath) ? null : MenuInfoJsonPath,
            ComnCdInfoPath = string.IsNullOrWhiteSpace(ComnCdInfoPath) ? null : ComnCdInfoPath,
            ComnCdDetailPath = string.IsNullOrWhiteSpace(ComnCdDetailPath) ? null : ComnCdDetailPath,
            TableInfoJsonPath = string.IsNullOrWhiteSpace(TableInfoJsonPath) ? null : TableInfoJsonPath,
            ColumnInfoJsonPath = string.IsNullOrWhiteSpace(ColumnInfoJsonPath) ? null : ColumnInfoJsonPath,
            IndexInfoJsonPath = string.IsNullOrWhiteSpace(IndexInfoJsonPath) ? null : IndexInfoJsonPath,
            TableUseObjectJsonPath = string.IsNullOrWhiteSpace(TableUseObjectJsonPath) ? null : TableUseObjectJsonPath,

            // 지정한 키만 저장한다. 비워둔 키까지 담으면 설정 파일만 지저분해진다.
            FunctionKeyMenus = FunctionKeyBindings
                .Where(binding => binding.HasMenu)
                .ToDictionary(binding => binding.KeyName, binding => binding.MenuTitle),

            Extract = new ExtractSettings
            {
                Host = string.IsNullOrWhiteSpace(ExtractHost) ? null : ExtractHost.Trim(),
                Port = ExtractPort,
                ServiceName = string.IsNullOrWhiteSpace(ExtractServiceName) ? null : ExtractServiceName.Trim(),
                UserId = string.IsNullOrWhiteSpace(ExtractUserId) ? null : ExtractUserId.Trim(),
                Password = string.IsNullOrEmpty(ExtractPassword) ? null : ExtractPassword,
                QueriesJsonPath = string.IsNullOrWhiteSpace(ExtractQueriesJsonPath) ? null : ExtractQueriesJsonPath,
                OutputFolder = string.IsNullOrWhiteSpace(ExtractOutputFolder) ? null : ExtractOutputFolder,
                // 제한 시간은 화면에 노출하지 않고 기존 값을 유지한다. (바꿀 일이 드물어 settings.json 에서 직접 고친다)
                CommandTimeoutSeconds = _settingsService.Current.Extract.CommandTimeoutSeconds,
            },
        };

        _settingsService.Save(settings);

        StatusMessage = "설정을 저장했습니다. 다음 실행부터 적용됩니다.";
    }
}
