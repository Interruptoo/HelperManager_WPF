using Microsoft.Extensions.DependencyInjection;

namespace HelperManager.Modules.ComnCode;

/// <summary>"Common Code" 화면을 메인 메뉴에 등록하기 위한 모듈 정의입니다.</summary>
public sealed class ComnCodeModule : IFeatureModule
{
    public string Title => "Common Code";

    public string IconGlyph => "🏷";

    public int Order => 40;

    public object CreateViewModel(IServiceProvider services) =>
        services.GetRequiredService<ComnCodeViewModel>();
}
