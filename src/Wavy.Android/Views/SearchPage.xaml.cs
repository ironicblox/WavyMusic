using Wavy.Android.ViewModels;
using Wavy.Core.Models;

namespace Wavy.Android.Views;

public partial class SearchPage : ContentPage
{
    private readonly SearchAndroidViewModel _vm;

    public SearchPage(SearchAndroidViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.NewTextValue)) _vm.Clear();
    }

    private void OnFilterSongs(object s, EventArgs e)    => _vm.SelectedFilter = SearchFilter.Songs;
    private void OnFilterArtists(object s, EventArgs e)  => _vm.SelectedFilter = SearchFilter.Artists;
    private void OnFilterAlbums(object s, EventArgs e)   => _vm.SelectedFilter = SearchFilter.Albums;
    private void OnFilterPlaylists(object s, EventArgs e)=> _vm.SelectedFilter = SearchFilter.Playlists;
}
