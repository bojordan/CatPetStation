using System.Windows;
using System.Windows.Media.Imaging;

namespace CatPetStation.App;

/// <summary>
/// The transparent, borderless, always-on-top window one pet lives in.
/// It never takes keyboard focus and never appears in the taskbar or
/// Alt+Tab — a pet should decorate the desktop, not get in the way.
/// </summary>
public partial class PetWindow : Window
{
    public PetWindow()
    {
        InitializeComponent();
    }

    public void SetFrame(BitmapSource frame, bool flipHorizontal)
    {
        if (!ReferenceEquals(Sprite.Source, frame))
            Sprite.Source = frame;
        var scaleX = flipHorizontal ? -1 : 1;
        if (Flip.ScaleX != scaleX)
            Flip.ScaleX = scaleX;
    }
}
