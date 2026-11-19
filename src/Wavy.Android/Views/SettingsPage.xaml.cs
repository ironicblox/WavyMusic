using Wavy.Android.ViewModels;

namespace Wavy.Android.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsAndroidViewModel _vm;

    public SettingsPage(SettingsAndroidViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    private async void OnClearCacheClicked(object s, EventArgs e)
    {
        var msg = _vm.ClearCache();
        await DisplayAlert("Done", msg, "OK");
    }
}
