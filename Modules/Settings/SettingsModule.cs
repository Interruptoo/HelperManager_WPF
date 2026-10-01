using Microsoft.Extensions.DependencyInjection;

namespace HelperManager.Modules.Settings;

/// <summary>"Settings" 화면을 메인 메뉴에 등록하기 위한 모듈 정의입니다.</summary>
public sealed class SettingsModule : IFeatureModule
{
    public string Title => "Settings";

    public string IconGlyph => "⚙";

    // 다른 작업 화면들보다 뒤쪽(맨 아래)에 위치하도록 큰 값을 준다.
    public int Order => 100;

    public object CreateViewModel(IServiceProvider services) =>
        services.GetRequiredService<SettingsViewModel>();
}
