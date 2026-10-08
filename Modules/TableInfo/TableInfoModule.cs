using Microsoft.Extensions.DependencyInjection;

namespace HelperManager.Modules.TableInfo;

/// <summary>"Table Info" 화면을 메인 메뉴에 등록하기 위한 모듈 정의입니다.</summary>
public sealed class TableInfoModule : IFeatureModule
{
    public string Title => "Table Info";

    public string IconGlyph => "🗄";

    public int Order => 50;

    public object CreateViewModel(IServiceProvider services) =>
        services.GetRequiredService<TableInfoViewModel>();
}
