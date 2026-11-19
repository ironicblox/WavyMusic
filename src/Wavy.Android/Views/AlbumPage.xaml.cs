using Wavy.Android.ViewModels;

namespace Wavy.Android.Views;

[QueryProperty(nameof(BrowseId), "browseId")]
public partial class AlbumPage : ContentPage
{
    private readonly AlbumAndroidViewModel _vm;

    public string? BrowseId
    {
        set { _vm.BrowseId = value; _ = _vm.LoadAsync(); }
    }

    public AlbumPage(AlbumAndroidViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }
}
