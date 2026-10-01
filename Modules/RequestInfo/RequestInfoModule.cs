using Microsoft.Extensions.DependencyInjection;

namespace HelperManager.Modules.RequestInfo;

/// <summary>
/// "RequestInfo" 화면을 메인 메뉴에 등록하기 위한 모듈 정의입니다.
/// 앞으로 새 화면을 추가할 때 이 클래스를 그대로 복사해서 이름/아이콘/순서만 바꾸면 됩니다.
/// </summary>
public sealed class RequestInfoModule : IFeatureModule
{
    public string Title => "Request LogViewer";

    public string IconGlyph => "📄";

    public int Order => 10;

    public object CreateViewModel(IServiceProvider services) =>
        services.GetRequiredService<RequestInfoViewModel>();
}
