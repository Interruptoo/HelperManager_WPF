namespace HelperManager.Modules;

/// <summary>
/// 메인 화면의 메뉴(또는 탭)에 등록되는 "기능 화면 하나"를 표현하는 인터페이스입니다.
/// 앞으로 새로운 화면을 추가하고 싶을 때는 이 인터페이스를 구현한 클래스를 하나 만들고
/// App.xaml.cs 의 서비스 등록부에 한 줄만 추가하면 메인 메뉴에 자동으로 나타납니다.
/// (MainViewModel 이나 MainWindow 를 직접 수정할 필요가 없도록 설계했습니다.)
/// </summary>
public interface IFeatureModule
{
    /// <summary>메인 화면 좌측 메뉴(또는 탭)에 표시될 이름입니다.</summary>
    string Title { get; }

    /// <summary>메뉴 항목 앞에 표시할 간단한 아이콘 문자(이모지 또는 심볼)입니다.</summary>
    string IconGlyph { get; }

    /// <summary>
    /// 메뉴 정렬 순서입니다. 값이 작을수록 위쪽(또는 앞쪽)에 표시됩니다.
    /// 화면이 여러 개로 늘어났을 때 순서를 제어하기 위한 용도입니다.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// 이 메뉴가 선택되었을 때 실제로 화면에 표시할 ViewModel 인스턴스를 생성합니다.
    /// ViewModel 은 DI 컨테이너(<paramref name="services"/>)를 통해 생성되므로,
    /// 생성자 주입으로 필요한 서비스를 자유롭게 받을 수 있습니다.
    /// </summary>
    object CreateViewModel(IServiceProvider services);
}
