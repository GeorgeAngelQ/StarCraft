using StarCraft.Data;
using StarCraft.Models;
using Microsoft.EntityFrameworkCore;

namespace StarCraft.Views;

public partial class SeriesPage : ContentPage
{
    private List<Serie> series = new();
    private List<Serie> seriesFiltradas = new();

    // Paginación
    private int paginaActual = 1;
    private int itemsPorPagina = 5;
    private int totalPaginas = 1;

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
        try
        {
            var db = new AppDbContext();
            series = await db.Series
                .Include(s => s.Jugador1)
                .Include(s => s.Jugador2)
                .OrderByDescending(s => s.Fecha)
                .ThenByDescending(s => s.IdSerie)
                .ToListAsync();
            await db.DisposeAsync();

            seriesFiltradas = series;
            paginaActual = 1;
            ActualizarPaginacion();
        }
        catch (Exception ex)
        {
            await DisplayAlert("❌ Error",
                $"No se pudo cargar la lista de series: {ex.Message}", "OK");
        }
    }

    private void ActualizarPaginacion()
    {
        totalPaginas = (int)Math.Ceiling(seriesFiltradas.Count / (double)itemsPorPagina);

        if (totalPaginas == 0) totalPaginas = 1;
        if (paginaActual > totalPaginas) paginaActual = totalPaginas;

        var itemsPagina = seriesFiltradas
            .Skip((paginaActual - 1) * itemsPorPagina)
            .Take(itemsPorPagina)
            .ToList();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            SeriesCollection.ItemsSource = itemsPagina;
            ActualizarControlesPaginacion();
        });
    }

    private void ActualizarControlesPaginacion()
    {
        LblPaginaActual.Text = $"Página {paginaActual} de {totalPaginas}";
        LblTotalRegistros.Text = $"Total: {seriesFiltradas.Count} serie(s)";

        BtnPrimeraPagina.IsEnabled = paginaActual > 1;
        BtnAnterior.IsEnabled = paginaActual > 1;
        BtnSiguiente.IsEnabled = paginaActual < totalPaginas;
        BtnUltimaPagina.IsEnabled = paginaActual < totalPaginas;

        BtnPrimeraPagina.Opacity = BtnPrimeraPagina.IsEnabled ? 1.0 : 0.4;
        BtnAnterior.Opacity = BtnAnterior.IsEnabled ? 1.0 : 0.4;
        BtnSiguiente.Opacity = BtnSiguiente.IsEnabled ? 1.0 : 0.4;
        BtnUltimaPagina.Opacity = BtnUltimaPagina.IsEnabled ? 1.0 : 0.4;
    }

    private async void OnAgregarClicked(object sender, EventArgs e)
    {
        try
        {
            await Navigation.PushAsync(new SerieFormPage());
        }
        catch (Exception ex)
        {
            await DisplayAlert("❌ Error",
                $"No se pudo abrir el formulario: {ex.Message}", "OK");
        }
    }

    private async void OnSerieSeleccionada(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.CurrentSelection.FirstOrDefault() is Serie serie)
            {
                await Navigation.PushAsync(new SerieFormPage(serie.IdSerie));

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ((CollectionView)sender).SelectedItem = null;
                });
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("❌ Error",
                $"No se pudo abrir la serie: {ex.Message}", "OK");
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        try
        {
            string texto = e.NewTextValue?.ToLower() ?? "";

            seriesFiltradas = string.IsNullOrEmpty(texto)
                ? series
                : series.Where(s =>
                    s.Modalidad.ToLower().Contains(texto) ||
                    s.Jugador1.Alias.ToLower().Contains(texto) ||
                    s.Jugador2.Alias.ToLower().Contains(texto) ||
                    s.Fecha.ToString("dd/MM/yyyy").Contains(texto)
                ).ToList();

            paginaActual = 1;
            ActualizarPaginacion();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error en búsqueda: {ex.Message}");
        }
    }

    // Métodos de paginación
    private void OnPrimeraPaginaClicked(object sender, EventArgs e)
    {
        paginaActual = 1;
        ActualizarPaginacion();
    }

    private void OnAnteriorClicked(object sender, EventArgs e)
    {
        if (paginaActual > 1)
        {
            paginaActual--;
            ActualizarPaginacion();
        }
    }

    private void OnSiguienteClicked(object sender, EventArgs e)
    {
        if (paginaActual < totalPaginas)
        {
            paginaActual++;
            ActualizarPaginacion();
        }
    }

    private void OnUltimaPaginaClicked(object sender, EventArgs e)
    {
        paginaActual = totalPaginas;
        ActualizarPaginacion();
    }

    private void OnItemsPorPaginaChanged(object sender, EventArgs e)
    {
        if (PickerItemsPorPagina.SelectedItem is string seleccion)
        {
            if (int.TryParse(seleccion, out int cantidad))
            {
                itemsPorPagina = cantidad;
                paginaActual = 1;
                ActualizarPaginacion();
            }
        }
    }
}