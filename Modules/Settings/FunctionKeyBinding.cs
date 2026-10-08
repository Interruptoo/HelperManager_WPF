using CommunityToolkit.Mvvm.ComponentModel;

namespace HelperManager.Modules.Settings;

/// <summary>
/// Settings 화면의 "기능키 단축키" 목록에서 한 줄(F1 하나)을 나타냅니다.
/// 어떤 기능키에 어떤 메뉴를 연결할지를 들고 있다.
/// </summary>
public sealed partial class FunctionKeyBinding : ObservableObject
{
    /// <summary>지정하지 않은 상태를 콤보박스에 보여줄 때 쓰는 항목 이름.</summary>
    public const string NoneLabel = "(지정 안 함)";

    /// <summary>기능키 이름. "F1" ~ "F12".</summary>
    public required string KeyName { get; init; }

    /// <summary>
    /// 이 키를 눌렀을 때 이동할 메뉴 이름(IFeatureModule.Title).
    /// 지정하지 않았으면 <see cref="NoneLabel"/>.
    /// </summary>
    [ObservableProperty]
    private string menuTitle = NoneLabel;

    /// <summary>실제로 메뉴가 지정되어 있는지. (설정을 저장할 때 걸러내는 용도)</summary>
    public bool HasMenu => MenuTitle != NoneLabel && !string.IsNullOrWhiteSpace(MenuTitle);
}
