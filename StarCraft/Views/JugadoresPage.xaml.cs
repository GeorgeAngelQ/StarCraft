using StarCraft.Data;
using StarCraft.Models;
using Microsoft.EntityFrameworkCore;

namespace StarCraft.Views;

public partial class JugadoresPage : ContentPage
{
    private List<Jugador> jugadores = new();
    private Jugador? jugadorEditando = null;

    public JugadoresPage()
    {
        InitializeComponent();
        CargarJugadores();
    }

    private async void CargarJugadores()
    {
        using var db = new AppDbContext();
        jugadores = await db.Jugadores.OrderBy(j => j.Alias).ToListAsync();
        JugadoresCollection.ItemsSource = jugadores;
    }
    private void Campo_Completed(object sender, EventArgs e)
    {
        OnGuardarClicked(sender, e);
    }
    private async void OnGuardarClicked(object sender, EventArgs e)
    {
        try
        {
            using var db = new AppDbContext();

            string alias = AliasEntry.Text?.Trim() ?? string.Empty;
            string pais = PaisEntry.Text?.Trim() ?? string.Empty;
            string? raza = string.IsNullOrWhiteSpace(RazaPicker.SelectedItem as string)
                ? null
                : RazaPicker.SelectedItem as string;

            if (string.IsNullOrWhiteSpace(alias))
            {
                await DisplayAlert("Error", "El alias es obligatorio.", "OK");
                return;
            }

            bool aliasExiste = await db.Jugadores
                .AnyAsync(j => j.Alias.ToLower() == alias.ToLower() &&
                               (jugadorEditando == null || j.IdJugador != jugadorEditando.IdJugador));

            if (aliasExiste)
            {
                await DisplayAlert("Duplicado", $"El alias '{alias}' ya está registrado.", "OK");
                return;
            }

            if (jugadorEditando == null)
            {
                var nuevo = new Jugador
                {
                    Alias = alias,
                    Pais = string.IsNullOrEmpty(pais) ? null : pais,
                    RazaPrincipal = raza
                };

                db.Jugadores.Add(nuevo);
            }
            else
            {
                jugadorEditando.Alias = alias;
                jugadorEditando.Pais = string.IsNullOrEmpty(pais) ? null : pais;
                jugadorEditando.RazaPrincipal = raza;

                db.Jugadores.Update(jugadorEditando);
                jugadorEditando = null;
                await DisplayAlert("Actualizado", "Jugador actualizado correctamente.", "OK");
            }

            await db.SaveChangesAsync();

            LimpiarFormulario();
            CargarJugadores();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Ocurrió un error al guardar el jugador.\n\n{ex.Message}", "OK");
        }
    }


    private void LimpiarFormulario()
{
    AliasEntry.Text = "";
    PaisEntry.Text = "";
    RazaPicker.SelectedIndex = -1;
    jugadorEditando = null;
}

private async void OnEditarClicked(object sender, EventArgs e)
{
    var jugador = (sender as Button)?.CommandParameter as Jugador;
    if (jugador == null) return;

    jugadorEditando = jugador;
    AliasEntry.Text = jugador.Alias;
    PaisEntry.Text = jugador.Pais;
    RazaPicker.SelectedItem = jugador.RazaPrincipal;
}


    private async void OnEliminarClicked(object sender, EventArgs e)
    {
        var jugador = (sender as Button)?.CommandParameter as Jugador;
        if (jugador == null) return;

        bool confirmar = await DisplayAlert("Eliminar", $"¿Deseas eliminar a {jugador.Alias}?", "Sí", "No");
        if (!confirmar) return;

        using var db = new AppDbContext();
        db.Jugadores.Remove(jugador);
        await db.SaveChangesAsync();
        CargarJugadores();
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        string texto = e.NewTextValue?.ToLower() ?? "";
        JugadoresCollection.ItemsSource = jugadores
            .Where(j => j.Alias.ToLower().Contains(texto))
            .ToList();
    }

    private void OnJugadorSeleccionado(object sender, SelectionChangedEventArgs e)
    {
        JugadoresCollection.SelectedItem = null;
    }
}

