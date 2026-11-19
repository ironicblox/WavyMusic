using Wavy.Android.ViewModels;

namespace Wavy.Android.Views;

public partial class LyricsPage : ContentPage
{
    public LyricsPage(LyricsAndroidViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        _ = vm.ReloadAsync();
    }
}
