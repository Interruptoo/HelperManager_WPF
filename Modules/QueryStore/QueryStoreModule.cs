using Microsoft.Extensions.DependencyInjection;

namespace HelperManager.Modules.QueryStore;

/// <summary>
/// "Query Store" 화면을 메인 메뉴에 등록하기 위한 모듈 정의입니다.
/// </summary>
public sealed class QueryStoreModule : IFeatureModule
{
    public string Title => "Query Store";

    public string IconGlyph => "🗄";

    public int Order => 20;

    public object CreateViewModel(IServiceProvider services) =>
        services.GetRequiredService<QueryStoreViewModel>();
}
