using Microsoft.EntityFrameworkCore;
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
        DateTime fDesde = DateDesde.Date;
        DateTime fHasta = DateHasta.Date;

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

        query = query.Where(j => j.FechaCreacion.Date >= fDesde);
        query = query.Where(j => j.FechaCreacion.Date <= fHasta);

        var resultados = await query
            .OrderByDescending(j => j.FechaCreacion)
            .Select(j => new
            {
                SerieText = $"{j.Serie.Jugador1.Alias} vs {j.Serie.Jugador2.Alias} ({j.Serie.Modalidad})",
                JuegoText = $"Ganador: {j.Ganador.Alias} | Mapa: {j.Mapa.Nombre}",
                j.FechaCreacion
            })
            .ToListAsync();

        ListaResultados.ItemsSource = resultados;
    }
}
