using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using StarCraft.Data;
using StarCraft.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StarCraft.Views;

public partial class JuegosPage : ContentPage
{
    private readonly AppDbContext _db = new();
    private List<Serie> _series = new();
    private List<Mapa> _mapas = new();
    private List<Jugador> _jugadores = new();
    private List<Juego> _juegos = new();

    private Juego? _editingJuego = null;

    private class SeriesItem { public Serie Serie { get; set; } = null!; public string Display { get; set; } = string.Empty; }
    private class GenericItem<T> { public T Item { get; set; } = default!; public string Display { get; set; } = string.Empty; }

    public JuegosPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadReferencesAsync();
        await LoadJuegosAsync();
        SetFieldsEnabled(false);
    }

    private async Task LoadReferencesAsync()
    {
        _series = await _db.Series
            .Include(s => s.Jugador1)
            .Include(s => s.Jugador2)
            .OrderByDescending(s => s.Fecha)
            .ThenByDescending(s => s.IdSerie)
            .ToListAsync();

        var seriesItems = _series.Select(s => new SeriesItem
        {
            Serie = s,
            Display = $"{s.Modalidad} - {s.Jugador1?.Alias} vs {s.Jugador2?.Alias} ({s.Fecha:dd/MM/yyyy})"
        }).ToList();

        SeriePicker.ItemsSource = seriesItems;
        SeriePicker.ItemDisplayBinding = new Binding("Display");

        _mapas = await _db.Mapas.OrderBy(m => m.Nombre).ToListAsync();
        var mapaItems = _mapas.Select(m => new GenericItem<Mapa> { Item = m, Display = m.Nombre }).ToList();
        MapaPicker.ItemsSource = mapaItems;
        MapaPicker.ItemDisplayBinding = new Binding("Display");

        _jugadores = await _db.Jugadores.OrderBy(j => j.Alias).ToListAsync();

        var races = new List<string?> { null, "Terran", "Zerg", "Protoss" };
        Raza1Picker.ItemsSource = races;
        Raza2Picker.ItemsSource = races;
    }

    private async Task LoadJuegosAsync()
    {
        if (SeriePicker.SelectedItem is not SeriesItem selectedItem)
        {
            JuegosCollection.ItemsSource = null;
            return;
        }
        _juegos = await _db.Juegos
            .Include(j => j.Serie).ThenInclude(s => s.Jugador1)
            .Include(j => j.Serie).ThenInclude(s => s.Jugador2)
            .Include(j => j.Mapa)
            .Include(j => j.Ganador)
            .Where(j => j.IdSerie == selectedItem.Serie.IdSerie)
            .OrderByDescending(j => j.FechaCreacion)
            .ToListAsync();

        JuegosCollection.ItemsSource = _juegos.Select(j => new
        {
            j.IdJuego,
            SerieText = $"{j.Serie?.Modalidad} - {j.Serie?.Jugador1?.Alias} vs {j.Serie?.Jugador2?.Alias} ({j.Serie?.Fecha:dd/MM/yyyy})",
            MapaNombre = j.Mapa?.Nombre,
            j.RazaJugador1,
            j.RazaJugador2,
            GanadorAlias = j.Ganador?.Alias ?? string.Empty
        }).ToList();
    }

    private void SetFieldsEnabled(bool enabled)
    {
        MapaPicker.IsEnabled = enabled;
        GanadorPicker.IsEnabled = enabled;
        Raza1Picker.IsEnabled = enabled;
        Raza2Picker.IsEnabled = enabled;
    }

    private async void SeriePicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (SeriePicker.SelectedIndex == -1)
        {
            SetFieldsEnabled(false);
            return;
        }

        var selected = (SeriesItem)SeriePicker.SelectedItem;
        var serie = selected.Serie;

        SetFieldsEnabled(true);

        var winners = new List<GenericItem<Jugador>>();
        if (serie.Jugador1 != null) winners.Add(new GenericItem<Jugador> { Item = serie.Jugador1, Display = serie.Jugador1.Alias });
        if (serie.Jugador2 != null) winners.Add(new GenericItem<Jugador> { Item = serie.Jugador2, Display = serie.Jugador2.Alias });

        GanadorPicker.ItemsSource = winners;
        GanadorPicker.ItemDisplayBinding = new Binding("Display");
        GanadorPicker.SelectedIndex = -1;

        MapaPicker.SelectedIndex = -1;

        Raza1Picker.SelectedIndex = -1;
        Raza2Picker.SelectedIndex = -1;

        await LoadJuegosAsync();
    }
    private void Campo_Completed(object sender, EventArgs e)
    {
        GuardarButton_Clicked(sender, e);
    }
    private async void GuardarButton_Clicked(object sender, EventArgs e)
    {
        if (SeriePicker.SelectedIndex == -1)
        {
            await DisplayAlert("Error", "Selecciona una serie.", "OK");
            return;
        }
        if (MapaPicker.SelectedIndex == -1)
        {
            await DisplayAlert("Error", "Selecciona un mapa.", "OK");
            return;
        }
        if (GanadorPicker.SelectedIndex == -1)
        {
            await DisplayAlert("Error", "Selecciona el ganador (uno de los dos jugadores de la serie).", "OK");
            return;
        }

        var selectedSeriesItem = (SeriesItem)SeriePicker.SelectedItem;
        var selectedSerie = selectedSeriesItem.Serie;

        var selectedMapaItem = (GenericItem<Mapa>)MapaPicker.SelectedItem;
        var selectedMapa = selectedMapaItem.Item;

        var selectedGanadorItem = (GenericItem<Jugador>)GanadorPicker.SelectedItem;
        var selectedGanador = selectedGanadorItem.Item;

        string? raza1 = Raza1Picker.SelectedItem as string;
        string? raza2 = Raza2Picker.SelectedItem as string;
        raza1 = string.IsNullOrWhiteSpace(raza1) ? null : raza1;
        raza2 = string.IsNullOrWhiteSpace(raza2) ? null : raza2;

        if (_editingJuego == null)
        {
            var nuevo = new Juego
            {
                IdSerie = selectedSerie.IdSerie,
                IdMapa = selectedMapa.IdMapa,
                RazaJugador1 = raza1,
                RazaJugador2 = raza2,
                IdGanador = selectedGanador.IdJugador,
                FechaCreacion = DateTime.Now
            };

            _db.Juegos.Add(nuevo);
            await _db.SaveChangesAsync();
        }
        else
        {
            _editingJuego.IdSerie = selectedSerie.IdSerie;
            _editingJuego.IdMapa = selectedMapa.IdMapa;
            _editingJuego.RazaJugador1 = raza1;
            _editingJuego.RazaJugador2 = raza2;
            _editingJuego.IdGanador = selectedGanador.IdJugador;

            _db.Juegos.Update(_editingJuego);
            await _db.SaveChangesAsync();
            _editingJuego = null;
        }

        await LoadJuegosAsync();
        NuevoButton_Clicked(null, EventArgs.Empty); 
    }

    private void NuevoButton_Clicked(object? sender, EventArgs e)
    {
        SeriePicker.SelectedIndex = -1;
        MapaPicker.SelectedIndex = -1;
        GanadorPicker.ItemsSource = null;
        GanadorPicker.SelectedIndex = -1;
        Raza1Picker.SelectedIndex = -1;
        Raza2Picker.SelectedIndex = -1;
        SetFieldsEnabled(false);
        _editingJuego = null;
    }

    private async void EditarButton_Clicked(object sender, EventArgs e)
    {
        if (!(sender is Button btn) || !(btn.CommandParameter is int id)) return;

        var juego = await _db.Juegos
            .Include(j => j.Serie).ThenInclude(s => s.Jugador1)
            .Include(j => j.Serie).ThenInclude(s => s.Jugador2)
            .Include(j => j.Mapa)
            .Include(j => j.Ganador)
            .FirstOrDefaultAsync(j => j.IdJuego == id);

        if (juego == null) return;

        _editingJuego = juego;

        var seriesItems = (List<SeriesItem>)SeriePicker.ItemsSource!;
        var indexSerie = seriesItems.FindIndex(si => si.Serie.IdSerie == juego.IdSerie);
        if (indexSerie >= 0) SeriePicker.SelectedIndex = indexSerie;

        var mapaItems = (List<GenericItem<Mapa>>)MapaPicker.ItemsSource!;
        var indexMapa = mapaItems.FindIndex(mi => mi.Item.IdMapa == juego.IdMapa);
        if (indexMapa >= 0) MapaPicker.SelectedIndex = indexMapa;

        var winners = ((List<GenericItem<Jugador>>)GanadorPicker.ItemsSource!) ?? new List<GenericItem<Jugador>>();
        var idxWin = winners.FindIndex(w => w.Item.IdJugador == juego.IdGanador);
        if (idxWin >= 0) GanadorPicker.SelectedIndex = idxWin;

        Raza1Picker.SelectedItem = juego.RazaJugador1;
        Raza2Picker.SelectedItem = juego.RazaJugador2;
    }

    private async void EliminarButton_Clicked(object sender, EventArgs e)
    {
        if (!(sender is Button btn) || !(btn.CommandParameter is int id)) return;

        var confirm = await DisplayAlert("Confirmar", "¿Eliminar este juego?", "Sí", "No");
        if (!confirm) return;

        var juego = await _db.Juegos.FindAsync(id);
        if (juego == null) return;

        _db.Juegos.Remove(juego);
        await _db.SaveChangesAsync();
        await LoadJuegosAsync();
    }

    private void SearchBar_TextChanged(object sender, TextChangedEventArgs e)
    {
        var q = e.NewTextValue?.Trim().ToLower() ?? "";

        if (string.IsNullOrWhiteSpace(q))
        {
            JuegosCollection.ItemsSource = _juegos.Select(j => new
            {
                j.IdJuego,
                SerieText = $"{j.Serie?.Modalidad} - {j.Serie?.Jugador1?.Alias} vs {j.Serie?.Jugador2?.Alias} ({j.Serie?.Fecha:dd/MM/yyyy})",
                MapaNombre = j.Mapa?.Nombre,
                j.RazaJugador1,
                j.RazaJugador2,
                GanadorAlias = j.Ganador?.Alias ?? string.Empty
            }).ToList();
            return;
        }

        var filtered = _juegos.Where(j =>
            (j.Serie?.Modalidad?.ToLower().Contains(q) ?? false) ||
            (j.Mapa?.Nombre?.ToLower().Contains(q) ?? false) ||
            (j.RazaJugador1?.ToLower().Contains(q) ?? false) ||
            (j.RazaJugador2?.ToLower().Contains(q) ?? false) ||
            (j.Ganador?.Alias?.ToLower().Contains(q) ?? false) ||
            (j.Serie?.Jugador1?.Alias?.ToLower().Contains(q) ?? false) ||
            (j.Serie?.Jugador2?.Alias?.ToLower().Contains(q) ?? false)
        ).Select(j => new
        {
            j.IdJuego,
            SerieText = $"{j.Serie?.Modalidad} - {j.Serie?.Jugador1?.Alias} vs {j.Serie?.Jugador2?.Alias} ({j.Serie?.Fecha:dd/MM/yyyy})",
            MapaNombre = j.Mapa?.Nombre,
            j.RazaJugador1,
            j.RazaJugador2,
            GanadorAlias = j.Ganador?.Alias ?? string.Empty
        }).ToList();

        JuegosCollection.ItemsSource = filtered;
    }

    private void JuegosCollection_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ((CollectionView)sender).SelectedItem = null;
    }
}
