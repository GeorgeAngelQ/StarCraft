namespace StarCraft;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnSeriesClicked(object sender, EventArgs e) =>
        await Navigation.PushAsync(new Views.SeriesPage());

    private async void OnJuegosClicked(object sender, EventArgs e) =>
        await Navigation.PushAsync(new Views.JuegosPage());

    private async void OnJugadoresClicked(object sender, EventArgs e) =>
        await Navigation.PushAsync(new Views.JugadoresPage());

    private async void OnMapasClicked(object sender, EventArgs e) =>
        await Navigation.PushAsync(new Views.MapasPage());

}