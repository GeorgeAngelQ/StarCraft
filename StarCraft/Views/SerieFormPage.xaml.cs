using StarCraft.Data;
using StarCraft.Models;
using Microsoft.EntityFrameworkCore;

namespace StarCraft.Views;

public partial class SerieFormPage : ContentPage
{
    private readonly AppDbContext _context = new();
    private Serie _serie;

    public SerieFormPage(int? idSerie = null)
    {
        InitializeComponent();
        CargarJugadores();

        if (idSerie.HasValue)
            CargarSerie(idSerie.Value);
    }

    private async void CargarJugadores()
    {
        var jugadores = await _context.Jugadores
                        .OrderBy(j => j.Alias)
                        .ToListAsync();

        Jugador1Picker.ItemsSource = jugadores;
        Jugador1Picker.ItemDisplayBinding = new Binding("Alias");

        Jugador2Picker.ItemsSource = jugadores;
        Jugador2Picker.ItemDisplayBinding = new Binding("Alias");
    }

    private async void CargarSerie(int idSerie)
    {
        _serie = await _context.Series
            .Include(s => s.Jugador1)
            .Include(s => s.Jugador2)
            .FirstOrDefaultAsync(s => s.IdSerie == idSerie);

        if (_serie != null)
        {
            ModalidadEntry.Text = _serie.Modalidad;
            FechaPicker.Date = _serie.Fecha;

            Jugador1Picker.SelectedItem = await _context.Jugadores.FirstOrDefaultAsync(j => j.IdJugador == _serie.IdJugador1);
            Jugador2Picker.SelectedItem = await _context.Jugadores.FirstOrDefaultAsync(j => j.IdJugador == _serie.IdJugador2);
        }
    }
    private void Campo_Completed(object sender, EventArgs e)
    {
        OnGuardarClicked(sender, e);
    }
    private async void OnGuardarClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ModalidadEntry.Text))
        {
            await DisplayAlert("Error", "Debe ingresar la modalidad.", "OK");
            return;
        }

        var jugador1 = Jugador1Picker.SelectedItem as Jugador;
        var jugador2 = Jugador2Picker.SelectedItem as Jugador;

        if (_serie == null)
        {
            _serie = new Serie();
            _context.Series.Add(_serie);
        }

        _serie.Modalidad = ModalidadEntry.Text;
        _serie.Fecha = FechaPicker.Date;
        _serie.IdJugador1 = jugador1?.IdJugador ?? 0;
        _serie.IdJugador2 = jugador2?.IdJugador ?? 0;

        await _context.SaveChangesAsync();
        await Navigation.PopAsync();
    }

    private async void OnEliminarClicked(object sender, EventArgs e)
    {
        if (_serie == null)
        {
            await DisplayAlert("Error", "Debe seleccionar una serie existente.", "OK");
            return;
        }

        var confirm = await DisplayAlert("Confirmar", "¿Desea eliminar esta serie?", "Sí", "No");
        if (confirm)
        {
            _context.Series.Remove(_serie);
            await _context.SaveChangesAsync();
            await DisplayAlert("Éxito", "Serie eliminada.", "OK");
            await Navigation.PopAsync();
        }
    }
}
