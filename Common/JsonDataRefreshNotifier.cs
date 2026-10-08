namespace HelperManager.Common;

/// <summary>
/// JSON 파일을 새로 추출했다는 사실을 각 화면에 알려주는 통로입니다.
///
/// 화면 ViewModel 들은 싱글톤이라 생성자에서 파일을 한 번만 읽는다. 그래서 Settings 화면에서
/// [지금 추출]로 JSON 을 새로 받아도, 이미 열어본 적이 있는 화면은 예전 내용을 그대로 들고 있었다.
/// 각 화면에 [새로고침] 버튼을 두지 않기로 했으므로, 추출이 끝나면 이 통로로 알려서 알아서 다시
/// 읽도록 한다.
///
/// 구독은 화면 ViewModel 이 "처음 만들어질 때" 일어나므로, 아직 한 번도 열지 않은 화면은 구독도
/// 하지 않는다. 그런 화면은 어차피 처음 열릴 때 새 파일을 읽으므로 따로 알릴 필요가 없다.
/// </summary>
public interface IJsonDataRefreshNotifier
{
    /// <summary>JSON 파일이 새로 추출되었을 때 발생한다. 각 화면이 자신의 파일을 다시 읽는 용도.</summary>
    event Action? Refreshed;

    /// <summary>추출이 끝났음을 알린다.</summary>
    void NotifyRefreshed();
}

/// <summary><see cref="IJsonDataRefreshNotifier"/> 의 구현체입니다.</summary>
public sealed class JsonDataRefreshNotifier : IJsonDataRefreshNotifier
{
    public event Action? Refreshed;

    public void NotifyRefreshed() => Refreshed?.Invoke();
}
