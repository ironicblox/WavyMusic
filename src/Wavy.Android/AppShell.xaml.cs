namespace Wavy.Android;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("player", typeof(Views.PlayerPage));
        Routing.RegisterRoute("lyrics", typeof(Views.LyricsPage));
        Routing.RegisterRoute("artist", typeof(Views.ArtistPage));
        Routing.RegisterRoute("album", typeof(Views.AlbumPage));
    }
}
