using StarCraft.Data;
using StarCraft.Models;
using Microsoft.EntityFrameworkCore;

namespace StarCraft.Views;

public partial class SeriesPage : ContentPage
{
    private readonly AppDbContext _context = new();

    public SeriesPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CargarSeries();
    }

    private async Task CargarSeries()
    {
        var series = await _context.Series
            .Include(s => s.Jugador1)
            .Include(s => s.Jugador2)
            .OrderByDescending(s => s.IdSerie)
            .ToListAsync();

        SeriesCollection.ItemsSource = series;
    }

    private async void OnAgregarClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new SerieFormPage());
    }

    private async void OnSerieSeleccionada(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Serie serie)
        {
            await Navigation.PushAsync(new SerieFormPage(serie.IdSerie));
            ((CollectionView)sender).SelectedItem = null;
        }
    }
}
