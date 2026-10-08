namespace HelperManager.Settings;

/// <summary>
/// 설정 파일(앱 실행 폴더의 settings.json)에 저장되는 내용입니다.
/// 매번 폴더/파일을 다시 찾아 여는 수고를 덜기 위해, 자주 쓰는 경로를 미리 등록해둔다.
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// 프로그램을 시작할 때 Request LogViewer 화면에 미리 탭으로 열어둘 로그 폴더 경로 목록.
    /// 여러 개 등록할 수 있으며, 등록한 순서대로 Tab1, Tab2, ... 가 자동으로 만들어진다.
    /// </summary>
    public List<string> RequestLogViewerFolderPaths { get; set; } = [];

    /// <summary>프로그램을 시작할 때 Query Store 화면이 자동으로 읽어올 쿼리 모음 XML 파일 경로.</summary>
    public string? QueryStoreXmlPath { get; set; }

    /// <summary>프로그램을 시작할 때 MenuInfo 화면이 자동으로 읽어올 메뉴 정보 JSON 파일 경로.</summary>
    public string? MenuInfoJsonPath { get; set; }

    /// <summary>프로그램을 시작할 때 Common Code 화면이 자동으로 읽어올 ComnCdInfo.json 파일 경로.</summary>
    public string? ComnCdInfoPath { get; set; }

    /// <summary>프로그램을 시작할 때 Common Code 화면이 자동으로 읽어올 ComnCdDetail.json 파일 경로.</summary>
    public string? ComnCdDetailPath { get; set; }

    /// <summary>프로그램을 시작할 때 Table Info 화면이 자동으로 읽어올 테이블 목록 JSON 파일 경로.</summary>
    public string? TableInfoJsonPath { get; set; }

    /// <summary>프로그램을 시작할 때 Table Info 화면이 자동으로 읽어올 컬럼 목록 JSON 파일 경로.</summary>
    public string? ColumnInfoJsonPath { get; set; }

    /// <summary>프로그램을 시작할 때 Table Info 화면이 자동으로 읽어올 인덱스 목록 JSON 파일 경로.</summary>
    public string? IndexInfoJsonPath { get; set; }

    /// <summary>
    /// 프로그램을 시작할 때 Table Info 화면이 자동으로 읽어올, 테이블을 사용 중인 오브젝트 목록
    /// JSON 파일 경로.
    /// </summary>
    public string? TableUseObjectJsonPath { get; set; }
}
