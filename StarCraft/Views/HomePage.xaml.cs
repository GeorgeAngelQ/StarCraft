using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;
using StarCraft.Data;
using StarCraft.Models;

namespace StarCraft.Views;

public partial class HomePage : ContentPage
{
    private readonly AppDbContext _context;

    public HomePage()
    {
        InitializeComponent();
        _context = new AppDbContext();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Task.Delay(100);

        await CargarCombos();
    }

    private async Task CargarCombos()
    {
        PickerJugador1.ItemsSource = await _context.Jugadores.OrderBy(j => j.Alias).ToListAsync();
        PickerJugador2.ItemsSource = await _context.Jugadores.OrderBy(j => j.Alias).ToListAsync();
        PickerMapa.ItemsSource = await _context.Mapas.OrderBy(m => m.Nombre).ToListAsync();
    }

    private async void OnBuscarClicked(object sender, EventArgs e)
    {
        if (PickerJugador1.SelectedItem == null ||
            PickerJugador2.SelectedItem == null ||
            PickerMapa.SelectedItem == null)
        {
            await DisplayAlert("Error", "Jugador 1, Jugador 2 y Mapa son obligatorios.", "OK");
            return;
        }

        var idJ1 = ((Jugador)PickerJugador1.SelectedItem).IdJugador;
        var idJ2 = ((Jugador)PickerJugador2.SelectedItem).IdJugador;
        var idMapa = ((Mapa)PickerMapa.SelectedItem).IdMapa;

        if (idJ1 == idJ2)
        {
            await DisplayAlert("Error", "Los jugadores no pueden ser iguales.", "OK");
            return;
        }

        string modalidad = EntryModalidad.Text?.Trim() ?? "";
        DateTime? fDesde = DateDesde.Date;
        DateTime? fHasta = DateHasta.Date;

        var query = _context.Juegos
            .Include(j => j.Serie).ThenInclude(s => s.Jugador1)
            .Include(j => j.Serie).ThenInclude(s => s.Jugador2)
            .Include(j => j.Mapa)
            .Include(j => j.Ganador)
            .Where(j =>
                j.Serie.IdJugador1 == idJ1 &&
                j.Serie.IdJugador2 == idJ2 &&
                j.IdMapa == idMapa);

        if (!string.IsNullOrEmpty(modalidad))
            query = query.Where(j => j.Serie.Modalidad.Contains(modalidad));
        if (fDesde != null)
            query = query.Where(j => j.FechaCreacion.Date >= fDesde.Value);
        if (fHasta != null)
            query = query.Where(j => j.FechaCreacion.Date <= fHasta.Value);

        var juegos = await query
            .OrderByDescending(j => j.FechaCreacion)
            .ToListAsync();

        int total = juegos.Count;

        int winsJ1 = juegos.Count(j => j.IdGanador == idJ1);
        int winsJ2 = juegos.Count(j => j.IdGanador == idJ2);

        double pctJ1 = total == 0 ? 0 : (winsJ1 * 100.0 / total);
        double pctJ2 = total == 0 ? 0 : (winsJ2 * 100.0 / total);

        LblResumen.Text =
            $"Total juegos: {total}\n" +
            $"{((Jugador)PickerJugador1.SelectedItem).Alias}: {winsJ1} victorias ({pctJ1:0.0}%)\n" +
            $"{((Jugador)PickerJugador2.SelectedItem).Alias}: {winsJ2} victorias ({pctJ2:0.0}%)";

        // Crear series para LiveCharts (LiveChartsCore + SkiaSharp)
        var aliasJ1 = ((Jugador)PickerJugador1.SelectedItem).Alias;
        var aliasJ2 = ((Jugador)PickerJugador2.SelectedItem).Alias;

        var series = new List<ISeries>
{
            new ColumnSeries<double>
            {
                Values = new double[] { winsJ1, winsJ2 },
                Fill = new SolidColorPaint(SKColor.Parse("#00bcd4")),  // Color para el jugador 1
                Name = aliasJ1  // Nombre de la serie para el jugador 1
            }
        };

                chart.Series = series;
                chart.XAxes = new Axis[]
                {
            new Axis
            {
                Labels = new string[] { aliasJ1, aliasJ2 },
                Name = "Jugadores",  // Eje X con los nombres de los jugadores
                LabelsRotation = 0  // Rotación de las etiquetas si es necesario
            }
        };

        var resultados = juegos.Select(j => new
        {
            SerieText = $"{j.Serie.Jugador1.Alias} vs {j.Serie.Jugador2.Alias}  ({j.Serie.Modalidad})",
            JuegoText = $"Ganador: {j.Ganador.Alias} | Mapa: {j.Mapa.Nombre}",
            j.FechaCreacion
        }).ToList();

        ListaResultados.ItemsSource = resultados;
    }
}
