using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelperManager.Common;
using HelperManager.Modules.ComnCode.Models;
using HelperManager.Modules.ComnCode.Services;

namespace HelperManager.Modules.ComnCode;

/// <summary>
/// Common Code 화면(ComnCdInfo.json + ComnCdDetail.json 뷰어)의 ViewModel.
///
/// 화면 동작 흐름:
///  1) 두 파일의 경로는 Settings 화면에 등록해두고, 앱이 시작할 때 자동으로 읽는다.
///     (파일을 읽어 두는 일은 <see cref="IComnCodeDataProvider"/> 가 맡는다 — ComnCdDetail 은
///     16만 건이 넘어 약 500MB 를 차지하므로, Table Info 화면과 한 벌을 나눠 쓴다)
///  2) 검색창에 값을 입력하면 -> Info 목록의 어느 필드든 하나라도 부분 일치하면 그 행을 보여준다.
///  3) 왼쪽 Info 목록에서 행을 선택하면 -> table_name/hsp_tp_cd/comn_grp_cd 가 같은 Detail 레코드만
///     걸러서 오른쪽에 보여준다.
///
/// 두 파일에서 실제로 확인된 필드들을 ComnCodeRecord 의 고정 속성으로 노출해서, 매번 컬럼을 다시
/// 만들 필요 없는 고정 DataGrid로 빠르게 보여준다. (이전의 DataTable 기반 동적 컬럼 생성은
/// 필터링할 때마다 테이블을 다시 만들어야 해서 느렸다)
/// </summary>
public sealed partial class ComnCodeViewModel : ObservableObject
{
    private readonly IComnCodeDataProvider _dataProvider;

    public ComnCodeViewModel(IComnCodeDataProvider dataProvider, IJsonDataRefreshNotifier refreshNotifier)
    {
        _dataProvider = dataProvider;

        RefreshCommand = new RelayCommand(Reload);

        // Settings 화면에서 JSON 을 새로 추출하면 화면에 [새로고침] 버튼 없이도 알아서 다시 읽는다.
        refreshNotifier.Refreshed += Reload;

        Reload();
    }

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
    private string statusMessage = string.Empty;

    public IRelayCommand RefreshCommand { get; }

    partial void OnFilterKeywordChanged(string value) => ApplyFilter();

    partial void OnSelectedInfoRecordChanged(ComnCodeRecord? value) => UpdateDetailRecords();

    /// <summary>보관소에서 공통코드 목록을 다시 가져와 화면을 구성한다.</summary>
    private void Reload()
    {
        ApplyFilter();

        StatusMessage = _dataProvider.Groups.Count == 0
            ? "공통코드 파일을 불러오지 못했습니다. Settings 에서 경로를 확인해 주세요."
            : $"공통코드 그룹 {_dataProvider.Groups.Count}건, 상세 코드 {_dataProvider.Details.Count}건을 불러왔습니다.";
    }

    /// <summary>검색어(Info의 어느 필드든 부분 일치, OR 조건)를 기준으로 FilteredInfoRecords 를 다시 구성한다.</summary>
    private void ApplyFilter()
    {
        IEnumerable<ComnCodeRecord> query = _dataProvider.Groups;

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
        DetailRecords = SelectedInfoRecord is { } selected
            ? ComnCodeGroupMatch.DetailsOf(_dataProvider.Details, selected).ToList()
            : [];
    }
}
