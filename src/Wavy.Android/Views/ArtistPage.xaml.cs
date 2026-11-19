using Wavy.Android.ViewModels;

namespace Wavy.Android.Views;

[QueryProperty(nameof(BrowseId), "browseId")]
public partial class ArtistPage : ContentPage
{
    private readonly ArtistAndroidViewModel _vm;

    public string? BrowseId
    {
        set { _vm.BrowseId = value; _ = _vm.LoadAsync(); }
    }

    public ArtistPage(ArtistAndroidViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }
}
