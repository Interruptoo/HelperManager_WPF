namespace HelperManager.Settings;

/// <summary>
/// 앱 설정을 읽고 저장하는 서비스의 계약입니다. JSON 파일로 영속화한다.
/// Settings 화면뿐 아니라, 시작 시 미리 등록된 경로를 자동으로 불러와야 하는
/// RequestInfo(로그 뷰어)/QueryStore 화면에서도 이 서비스를 통해 설정을 읽는다.
/// </summary>
public interface ISettingsService
{
    /// <summary>현재 메모리에 올라와 있는 설정. 앱 시작 시 파일에서 한 번 읽어 캐시해둔다.</summary>
    AppSettings Current { get; }

    /// <summary>
    /// 설정 파일(settings.json)의 실제 경로. JSON 추출기(HelperManager.Extractor.exe)를 띄울 때
    /// "이 설정 파일을 읽어라"고 넘겨주기 위해 필요하다.
    /// </summary>
    string FilePath { get; }

    /// <summary>설정을 파일에 저장하고, Current 를 갱신한다.</summary>
    void Save(AppSettings settings);
}
