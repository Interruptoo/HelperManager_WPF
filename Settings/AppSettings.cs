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

    /// <summary>
    /// F1~F12 기능키에 어떤 메뉴를 연결할지. 키는 "F1".."F12", 값은 메뉴 이름
    /// (<c>IFeatureModule.Title</c>, 예: "Table Info")이다.
    ///
    /// 메뉴 순서가 아니라 키마다 따로 지정하는 방식이라, 메뉴가 늘거나 순서가 바뀌어도
    /// 손에 익은 단축키는 그대로 유지된다. 지정하지 않은 키는 목록에 담지 않는다.
    /// </summary>
    public Dictionary<string, string> FunctionKeyMenus { get; set; } = [];

    /// <summary>
    /// 각 화면이 읽는 JSON 파일들을 DB 에서 직접 뽑아오는 추출 기능의 설정.
    /// 본체(HelperManager.exe)는 DB 에 붙지 않고, 별도의 HelperManager.Extractor.exe 가
    /// 이 설정을 읽어 추출한다. (Settings 화면의 [지금 추출] 버튼이 그 exe 를 실행한다)
    /// </summary>
    public ExtractSettings Extract { get; set; } = new();
}

/// <summary>
/// JSON 추출기(HelperManager.Extractor.exe)가 쓰는 설정입니다.
/// 본체와 추출기가 같은 settings.json 을 공유하므로, 이 클래스는 양쪽에서 같이 컴파일된다.
///
/// ※ 비밀번호는 평문으로 저장된다. settings.json 은 앱 실행 폴더에 있고 .gitignore 로 저장소에
///   올라가지 않지만, 파일을 열면 그대로 보이므로 공용 PC 에서는 주의가 필요하다.
/// </summary>
public sealed class ExtractSettings
{
    /// <summary>Oracle 서버 호스트명 또는 IP.</summary>
    public string? Host { get; set; }

    /// <summary>Oracle 리스너 포트. 보통 1521.</summary>
    public int Port { get; set; } = 1521;

    /// <summary>접속할 서비스명(SERVICE_NAME). SID 를 쓰는 환경이면 SID 를 적어도 된다.</summary>
    public string? ServiceName { get; set; }

    /// <summary>접속 계정.</summary>
    public string? UserId { get; set; }

    /// <summary>접속 계정 비밀번호. (평문 저장)</summary>
    public string? Password { get; set; }

    /// <summary>실행할 쿼리 모음(QUERIES.json) 파일 경로.</summary>
    public string? QueriesJsonPath { get; set; }

    /// <summary>추출한 JSON 파일들을 저장할 폴더. 비워두면 QUERIES.json 이 있는 폴더에 쓴다.</summary>
    public string? OutputFolder { get; set; }

    /// <summary>쿼리 하나당 제한 시간(초). 0 이면 제한 없음.</summary>
    public int CommandTimeoutSeconds { get; set; } = 600;
}
