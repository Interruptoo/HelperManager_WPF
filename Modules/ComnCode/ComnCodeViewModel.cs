using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelperManager.Modules.ComnCode.Models;
using HelperManager.Modules.ComnCode.Services;
using HelperManager.Settings;
using Microsoft.Win32;

namespace HelperManager.Modules.ComnCode;

/// <summary>
/// Common Code 화면(ComnCdInfo.json + ComnCdDetail.json 뷰어)의 ViewModel.
///
/// 화면 동작 흐름:
///  1) [파일 열기] 두 번(또는 Settings에 미리 등록해둔 경로로 시작 시 자동 로드)으로 두 JSON 파일을
///     모두 불러오면 -> Info 목록(왼쪽)이 채워진다. 두 파일에서 실제로 확인된 필드들을
///     ComnCodeRecord 의 고정 속성으로 노출해서, 매번 컬럼을 다시 만들 필요 없는 고정 DataGrid로
///     빠르게 보여준다. (이전의 DataTable 기반 동적 컬럼 생성은 필터링할 때마다 테이블을 다시
///     만들어야 해서 느렸다)
///  2) 검색창에 값을 입력하면 -> Info 목록의 어느 필드든 하나라도 부분 일치하면 그 행을 보여준다.
///  3) 왼쪽 Info 목록에서 행을 선택하면 -> table_name/hsp_tp_cd/comn_grp_cd 가 같은 Detail 레코드만
///     걸러서 오른쪽에 보여준다.
/// </summary>
public sealed partial class ComnCodeViewModel : ObservableObject
{
    private readonly IComnCodeParserService _parserService;

    private IReadOnlyList<ComnCodeRecord> _infoRecords = [];
    private IReadOnlyList<ComnCodeRecord> _detailRecords = [];

    public ComnCodeViewModel(IComnCodeParserService parserService, ISettingsService settingsService)
    {
        _parserService = parserService;

        OpenInfoFileCommand = new RelayCommand(OpenInfoFile);
        OpenDetailFileCommand = new RelayCommand(OpenDetailFile);
        RefreshCommand = new RelayCommand(Refresh, CanRefresh);

        var savedInfoPath = settingsService.Current.ComnCdInfoPath;
        var savedDetailPath = settingsService.Current.ComnCdDetailPath;
        if (!string.IsNullOrWhiteSpace(savedInfoPath) && File.Exists(savedInfoPath)
            && !string.IsNullOrWhiteSpace(savedDetailPath) && File.Exists(savedDetailPath))
        {
            LoadFiles(savedInfoPath, savedDetailPath);
        }
    }

    /// <summary>현재 불러온 ComnCdInfo.json 파일 경로.</summary>
    [ObservableProperty]
    private string? infoFilePath;

    /// <summary>현재 불러온 ComnCdDetail.json 파일 경로.</summary>
    [ObservableProperty]
    private string? detailFilePath;

    /// <summary>왼쪽 DataGrid에 표시되는, 검색어가 적용된 Info 목록.</summary>
    [ObservableProperty]
    private IReadOnlyList<ComnCodeRecord> filteredInfoRecords = [];

    /// <summary>왼쪽 DataGrid에서 선택된 Info 레코드. 여기서 조인 키를 뽑아 Detail을 필터링한다.</summary>
    [ObservableProperty]
    private ComnCodeRecord? selectedInfoRecord;

    /// <summary>오른쪽 DataGrid에 표시되는, 선택된 Info 레코드와 같은 그룹의 Detail 레코드 목록.</summary>
    [ObservableProperty]
    private IReadOnlyList<ComnCodeRecord> detailRecords = [];

    /// <summary>검색어. Info 목록의 필드 중 하나라도 부분 일치하면 그 행을 보여준다.</summary>
    [ObservableProperty]
    private string filterKeyword = string.Empty;

    /// <summary>화면 하단 상태 메시지.</summary>
    [ObservableProperty]
    private string statusMessage = "ComnCdInfo.json / ComnCdDetail.json 파일을 열어주세요.";

    public IRelayCommand OpenInfoFileCommand { get; }

    public IRelayCommand OpenDetailFileCommand { get; }

    public IRelayCommand RefreshCommand { get; }

    partial void OnFilterKeywordChanged(string value) => ApplyFilter();

    partial void OnSelectedInfoRecordChanged(ComnCodeRecord? value) => UpdateDetailRecords();

    private bool CanRefresh() => !string.IsNullOrWhiteSpace(InfoFilePath) && !string.IsNullOrWhiteSpace(DetailFilePath);

    private void OpenInfoFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "ComnCdInfo.json 파일을 선택하세요",
            Filter = "JSON 파일 (*.json)|*.json|모든 파일 (*.*)|*.*",
        };

        if (dialog.ShowDialog() == true)
        {
            InfoFilePath = dialog.FileName;
            TryLoadIfBothPathsReady();
        }
    }

    private void OpenDetailFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "ComnCdDetail.json 파일을 선택하세요",
            Filter = "JSON 파일 (*.json)|*.json|모든 파일 (*.*)|*.*",
        };

        if (dialog.ShowDialog() == true)
        {
            DetailFilePath = dialog.FileName;
            TryLoadIfBothPathsReady();
        }
    }

    private void TryLoadIfBothPathsReady()
    {
        if (!string.IsNullOrWhiteSpace(InfoFilePath) && !string.IsNullOrWhiteSpace(DetailFilePath))
        {
            LoadFiles(InfoFilePath, DetailFilePath);
        }
    }

    /// <summary>현재 불러온 두 파일을 디스크에서 다시 읽어온다. (정기적으로 갱신되는 파일을 위한 새로고침)</summary>
    private void Refresh()
    {
        if (CanRefresh())
        {
            LoadFiles(InfoFilePath!, DetailFilePath!);
        }
    }

    /// <summary>
    /// 지정한 두 JSON 파일을 대화상자 없이 바로 읽어 목록을 구성한다. Settings 화면에 미리 등록해둔
    /// 경로를 앱 시작 시 자동으로 불러올 때, [파일 열기]/[새로고침] 직후에도 쓰인다.
    /// </summary>
    private void LoadFiles(string infoPath, string detailPath)
    {
        InfoFilePath = infoPath;
        DetailFilePath = detailPath;

        _infoRecords = _parserService.ParseFile(infoPath);
        _detailRecords = _parserService.ParseFile(detailPath);

        ApplyFilter();

        StatusMessage = $"공통코드 그룹 {_infoRecords.Count}건, 상세 코드 {_detailRecords.Count}건을 불러왔습니다.";
        RefreshCommand.NotifyCanExecuteChanged();
    }

    /// <summary>검색어(Info의 어느 필드든 부분 일치, OR 조건)를 기준으로 FilteredInfoRecords 를 다시 구성한다.</summary>
    private void ApplyFilter()
    {
        IEnumerable<ComnCodeRecord> query = _infoRecords;

        if (!string.IsNullOrWhiteSpace(FilterKeyword))
        {
            var keyword = FilterKeyword.Trim();
            query = query.Where(record => record.Fields.Values.Any(value =>
                !string.IsNullOrEmpty(value) && value.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
        }

        FilteredInfoRecords = query.ToList();

        // 목록이 다시 그려지면 이전 선택은 더 이상 유효하지 않을 수 있으므로 비운다.
        SelectedInfoRecord = null;
        DetailRecords = [];
    }

    /// <summary>선택된 Info 레코드의 조인 키(table_name/hsp_tp_cd/comn_grp_cd)와 같은 Detail 레코드만 골라 보여준다.</summary>
    private void UpdateDetailRecords()
    {
        if (SelectedInfoRecord is not { } selected)
        {
            DetailRecords = [];
            return;
        }

        DetailRecords = _detailRecords
            .Where(record =>
                string.Equals(record.TableName, selected.TableName, StringComparison.OrdinalIgnoreCase) &&
                HspTpCdMatches(record.HspTpCd, selected.HspTpCd) &&
                string.Equals(record.ComnGrpCd, selected.ComnGrpCd, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// ComnCdDetail.json 을 뽑는 쿼리는 원본 테이블(CCCCCSTE/CCCMCSTE 등)과 상관없이 HSP_TP_CD를
    /// 항상 빈 값으로 고정해서 내보낸다. 반면 ComnCdInfo.json 쪽은 병원별 공통코드 테이블
    /// (예: CCCMCSTE)의 경우 실제 HSP_TP_CD 값('01' 등)이 들어있다. 그래서 두 값을 그대로 엄격히
    /// 비교하면, HSP_TP_CD가 채워져 있는 Info 그룹(CCCMCSTE 등)은 Detail과 절대 일치하지 않아
    /// 상세 목록이 비어 보이는 문제가 있었다. 둘 중 하나라도 값이 없으면(= 어느 쪽이든 구분하지
    /// 않는다는 의미로 보고) 그 항목은 조건을 통과시킨다.
    /// </summary>
    private static bool HspTpCdMatches(string? detailValue, string? infoValue)
    {
        if (string.IsNullOrEmpty(detailValue) || string.IsNullOrEmpty(infoValue))
        {
            return true;
        }

        return string.Equals(detailValue, infoValue, StringComparison.OrdinalIgnoreCase);
    }
}
