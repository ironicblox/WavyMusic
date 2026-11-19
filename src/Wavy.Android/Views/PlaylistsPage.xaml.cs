using Wavy.Android.ViewModels;

namespace Wavy.Android.Views;

public partial class PlaylistsPage : ContentPage
{
    private readonly PlaylistsAndroidViewModel _vm;

    public PlaylistsPage(PlaylistsAndroidViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    private async void OnCreateClicked(object s, EventArgs e)
    {
        var name = await DisplayPromptAsync("New Playlist", "Give it a name:", initialValue: _vm.SuggestedName);
        if (!string.IsNullOrWhiteSpace(name)) _vm.Create(name);
    }

    private async void OnImportLinkClicked(object s, EventArgs e)
    {
        var link = await DisplayPromptAsync("Import Playlist", "Paste a YouTube Music or Spotify link:");
        if (string.IsNullOrWhiteSpace(link)) return;
        _vm.IsImporting = true;
        var err = await _vm.ImportAsync(link);
        _vm.IsImporting = false;
        if (err is not null) await DisplayAlert("Import failed", err, "OK");
    }

    private async void OnShareCodeClicked(object s, EventArgs e)
    {
        var code = _vm.GetShareCode();
        if (code is null) { await DisplayAlert("Nothing to share", "Select a playlist with songs first.", "OK"); return; }
        await Clipboard.Default.SetTextAsync(code);
        await DisplayAlert("Code copied!", "Share this code with anyone:\n\n" + code, "OK");
    }

    private async void OnImportCodeClicked(object s, EventArgs e)
    {
        var code = await DisplayPromptAsync("Import by code", "Paste a Wavy share code:");
        if (string.IsNullOrWhiteSpace(code)) return;
        var err = _vm.ImportFromCode(code);
        if (err is not null) await DisplayAlert("Import failed", err, "OK");
        else await DisplayAlert("Imported!", $"Playlist \"{_vm.Selected?.Name}\" imported.", "OK");
    }

    private async void OnRenameClicked(object s, EventArgs e)
    {
        if (_vm.Selected is null) return;
        var name = await DisplayPromptAsync("Rename", "New name:", initialValue: _vm.Selected.Name);
        if (!string.IsNullOrWhiteSpace(name)) _vm.Rename(name);
    }

    private async void OnDeleteClicked(object s, EventArgs e)
    {
        if (_vm.Selected is null) return;
        var ok = await DisplayAlert("Delete", $"Delete \"{_vm.Selected.Name}\"?", "Delete", "Cancel");
        if (ok) _vm.DeleteSelectedCommand.Execute(null);
    }
}
