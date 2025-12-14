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
    private bool _isSearching = false;

    public HomePage()
    {
        InitializeComponent();

        // Configurar el gráfico vacío inicialmente
        ConfigurarGraficoVacio();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Task.Delay(100);
        await CargarCombos();
    }

    private void ConfigurarGraficoVacio()
    {
        chart.Series = new List<ISeries>();
        chart.XAxes = new Axis[]
        {
            new Axis
            {
                Labels = Array.Empty<string>(),
                TextSize = 14,
                LabelsPaint = new SolidColorPaint(SKColors.White)
            }
        };
        chart.YAxes = new Axis[]
        {
            new Axis
            {
                MinLimit = 0,
                TextSize = 14,
                LabelsPaint = new SolidColorPaint(SKColors.White)
            }
        };
    }

    private async Task CargarCombos()
    {
        try
        {
            var db = new AppDbContext();
            var jugadores = await db.Jugadores.OrderBy(j => j.Alias).ToListAsync();
            var mapas = await db.Mapas.OrderBy(m => m.Nombre).ToListAsync();
            await db.DisposeAsync();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                PickerJugador1.ItemsSource = jugadores;
                PickerJugador2.ItemsSource = jugadores;
                PickerMapa.ItemsSource = mapas;
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error al cargar datos: {ex.Message}", "OK");
        }
    }

    private async void OnBuscarClicked(object sender, EventArgs e)
    {
        if (_isSearching) return;

        try
        {
            _isSearching = true;
            ((Button)sender).IsEnabled = false;
            ((Button)sender).Text = "🔄 Buscando...";

            // Validaciones
            if (PickerJugador1.SelectedItem == null ||
                PickerJugador2.SelectedItem == null ||
                PickerMapa.SelectedItem == null)
            {
                await DisplayAlert("⚠️ Datos Incompletos",
                    "Por favor, selecciona ambos jugadores y un mapa.", "OK");
                return;
            }

            var jugador1 = (Jugador)PickerJugador1.SelectedItem;
            var jugador2 = (Jugador)PickerJugador2.SelectedItem;
            var mapa = (Mapa)PickerMapa.SelectedItem;

            if (jugador1.IdJugador == jugador2.IdJugador)
            {
                await DisplayAlert("⚠️ Error de Selección",
                    "Los jugadores deben ser diferentes.", "OK");
                return;
            }

            // Realizar búsqueda
            await RealizarBusqueda(jugador1, jugador2, mapa);
        }
        catch (Exception ex)
        {
            await DisplayAlert("❌ Error",
                $"Ocurrió un error durante la búsqueda: {ex.Message}", "OK");
        }
        finally
        {
            _isSearching = false;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ((Button)sender).IsEnabled = true;
                ((Button)sender).Text = "🔍 BUSCAR ENFRENTAMIENTOS";
            });
        }
    }

    private async Task RealizarBusqueda(Jugador jugador1, Jugador jugador2, Mapa mapa)
    {
        string modalidad = EntryModalidad.Text?.Trim() ?? "";
        DateTime? fDesde = DateDesde.Date;
        DateTime? fHasta = DateHasta.Date;

        var db = new AppDbContext();

        // Construir query
        var query = db.Juegos
            .Include(j => j.Serie).ThenInclude(s => s.Jugador1)
            .Include(j => j.Serie).ThenInclude(s => s.Jugador2)
            .Include(j => j.Mapa)
            .Include(j => j.Ganador)
            .Where(j =>
                ((j.Serie.IdJugador1 == jugador1.IdJugador && j.Serie.IdJugador2 == jugador2.IdJugador) ||
                 (j.Serie.IdJugador1 == jugador2.IdJugador && j.Serie.IdJugador2 == jugador1.IdJugador)) &&
                j.IdMapa == mapa.IdMapa);

        if (!string.IsNullOrEmpty(modalidad))
            query = query.Where(j => EF.Functions.Like(j.Serie.Modalidad, $"%{modalidad}%"));

        if (fDesde != null)
            query = query.Where(j => j.FechaCreacion.Date >= fDesde.Value);

        if (fHasta != null)
            query = query.Where(j => j.FechaCreacion.Date <= fHasta.Value);

        var juegos = await query
            .OrderByDescending(j => j.FechaCreacion)
            .ToListAsync();

        await db.DisposeAsync();

        // Calcular estadísticas
        int total = juegos.Count;

        if (total == 0)
        {
            await DisplayAlert("ℹ️ Sin Resultados",
                "No se encontraron juegos con los criterios especificados.", "OK");
            LimpiarResultados();
            return;
        }

        int winsJ1 = juegos.Count(j => j.IdGanador == jugador1.IdJugador);
        int winsJ2 = juegos.Count(j => j.IdGanador == jugador2.IdJugador);

        double pctJ1 = (winsJ1 * 100.0 / total);
        double pctJ2 = (winsJ2 * 100.0 / total);

        // Actualizar UI
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ActualizarResumen(jugador1, jugador2, total, winsJ1, winsJ2, pctJ1, pctJ2);
            ActualizarGrafico(jugador1, jugador2, winsJ1, winsJ2);
            ActualizarListaJuegos(juegos);
        });
    }

    private void ActualizarResumen(Jugador j1, Jugador j2, int total,
        int winsJ1, int winsJ2, double pctJ1, double pctJ2)
    {
        LblResumen.Text =
            $"📌 Total de juegos encontrados: {total}\n\n" +
            $"🏆 {j1.Alias}: {winsJ1} victorias ({pctJ1:0.0}%)\n" +
            $"🏆 {j2.Alias}: {winsJ2} victorias ({pctJ2:0.0}%)";
    }

    private void ActualizarGrafico(Jugador j1, Jugador j2, int winsJ1, int winsJ2)
    {
        var colorJ1 = winsJ1 >= winsJ2 ? SKColor.Parse("#6C63FF") : SKColor.Parse("#FF6B6B");
        var colorJ2 = winsJ2 >= winsJ1 ? SKColor.Parse("#6C63FF") : SKColor.Parse("#FF6B6B");

        chart.Series = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Values = new double[] { winsJ1 },
                Fill = new SolidColorPaint(colorJ1),
                Name = j1.Alias,
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsSize = 16,
                DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top
            },
            new ColumnSeries<double>
            {
                Values = new double[] { winsJ2 },
                Fill = new SolidColorPaint(colorJ2),
                Name = j2.Alias,
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsSize = 16,
                DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top
            }
        };

        chart.XAxes = new Axis[]
        {
            new Axis
            {
                Labels = new string[] { j1.Alias, j2.Alias },
                TextSize = 14,
                LabelsPaint = new SolidColorPaint(SKColors.White),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#3E3E42"))
            }
        };

        chart.YAxes = new Axis[]
        {
            new Axis
            {
                MinLimit = 0,
                TextSize = 14,
                LabelsPaint = new SolidColorPaint(SKColors.White),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#3E3E42"))
            }
        };

        chart.LegendPosition = LiveChartsCore.Measure.LegendPosition.Bottom;
        chart.LegendTextPaint = new SolidColorPaint(SKColors.White);
        chart.LegendTextSize = 14;
        chart.LegendBackgroundPaint = new SolidColorPaint(SKColor.Parse("#252529"));
    }

    private void ActualizarListaJuegos(List<Juego> juegos)
    {
        var resultados = juegos.Select(j => new
        {
            SerieText = $"{j.Serie.Jugador1.Alias} vs {j.Serie.Jugador2.Alias} — {j.Serie.Modalidad}",
            JuegoText = $"🏆 Ganador: {j.Ganador.Alias}  |  🗺️ Mapa: {j.Mapa.Nombre}",
            FechaCreacion = j.FechaCreacion
        }).ToList();

        ListaResultados.ItemsSource = resultados;
    }

    private void LimpiarResultados()
    {
        LblResumen.Text = string.Empty;
        ConfigurarGraficoVacio();
        ListaResultados.ItemsSource = null;
    }
}