using Wavy.Android.ViewModels;

namespace Wavy.Android.Views;

public partial class PlayerPage : ContentPage
{
    private readonly PlayerAndroidViewModel _vm;

    public PlayerPage(PlayerAndroidViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;

        // Keep play/pause button text in sync
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlayerAndroidViewModel.IsPlaying))
                PlayPauseBtn.Text = _vm.IsPlaying ? "⏸" : "▶";
        };
        PlayPauseBtn.Text = _vm.IsPlaying ? "⏸" : "▶";

        // Wire stream URL to MediaElement (community toolkit handles playback)
        _vm.StreamResolved += (_, url) =>
        {
            if (url is not null)
                // Navigate back if opened from elsewhere; audio continues via service
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // The actual MediaElement is on this page and plays via binding
                    // in a production build you'd drive it here
                });
        };
    }

    private async void OnLyricsClicked(object s, EventArgs e) =>
        await Shell.Current.GoToAsync("lyrics");

    private async void OnQueueClicked(object s, EventArgs e) =>
        await Shell.Current.GoToAsync("///library");
}
