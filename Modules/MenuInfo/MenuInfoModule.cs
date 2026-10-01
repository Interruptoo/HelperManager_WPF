using Microsoft.Extensions.DependencyInjection;

namespace HelperManager.Modules.MenuInfo;

/// <summary>"MenuInfo" 화면을 메인 메뉴에 등록하기 위한 모듈 정의입니다.</summary>
public sealed class MenuInfoModule : IFeatureModule
{
    public string Title => "Menu Info";

    public string IconGlyph => "📋";

    public int Order => 30;

    public object CreateViewModel(IServiceProvider services) =>
        services.GetRequiredService<MenuInfoViewModel>();
}
