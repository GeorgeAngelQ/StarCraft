using StarCraft.Data;
using StarCraft.Models;
using Microsoft.EntityFrameworkCore;

namespace StarCraft.Views;

public partial class SerieFormPage : ContentPage
{
    private Serie? _serie;
    private bool _modoEdicion = false;
    private bool _isGuardando = false;
    private int? _idSerieActual = null;

    public SerieFormPage(int? idSerie = null)
    {
        InitializeComponent();
        _modoEdicion = idSerie.HasValue;
        _idSerieActual = idSerie;

        if (_modoEdicion)
        {
            Title = "Editar Serie";
            LblTitulo.Text = "Editar Serie";
            BtnEliminar.IsVisible = true;
        }

        CargarJugadores();

        if (idSerie.HasValue)
            CargarSerie(idSerie.Value);
    }

    private async void CargarJugadores()
    {
        try
        {
            var db = new AppDbContext();
            var jugadores = await db.Jugadores
                .OrderBy(j => j.Alias)
                .ToListAsync();
            await db.DisposeAsync();

            if (jugadores.Count == 0)
            {
                await DisplayAlert("⚠️ Sin Jugadores",
                    "No hay jugadores registrados. Debe registrar al menos 2 jugadores antes de crear una serie.",
                    "OK");
                await Navigation.PopAsync();
                return;
            }

            if (jugadores.Count < 2)
            {
                await DisplayAlert("⚠️ Jugadores Insuficientes",
                    "Necesita al menos 2 jugadores registrados para crear una serie.",
                    "OK");
                await Navigation.PopAsync();
                return;
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Jugador1Picker.ItemsSource = jugadores;
                Jugador1Picker.ItemDisplayBinding = new Binding("Alias");
                Jugador2Picker.ItemsSource = jugadores;
                Jugador2Picker.ItemDisplayBinding = new Binding("Alias");
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("❌ Error",
                $"No se pudo cargar la lista de jugadores: {ex.Message}", "OK");
        }
    }

    private async void CargarSerie(int idSerie)
    {
        try
        {
            var db = new AppDbContext();
            _serie = await db.Series
                .Include(s => s.Jugador1)
                .Include(s => s.Jugador2)
                .FirstOrDefaultAsync(s => s.IdSerie == idSerie);

            if (_serie != null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ModalidadEntry.Text = _serie.Modalidad;
                    FechaPicker.Date = _serie.Fecha;
                });

                var jugador1 = await db.Jugadores
                    .FirstOrDefaultAsync(j => j.IdJugador == _serie.IdJugador1);
                var jugador2 = await db.Jugadores
                    .FirstOrDefaultAsync(j => j.IdJugador == _serie.IdJugador2);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Jugador1Picker.SelectedItem = jugador1;
                    Jugador2Picker.SelectedItem = jugador2;
                });
            }
            else
            {
                await DisplayAlert("❌ Error",
                    "No se encontró la serie especificada.", "OK");
                await Navigation.PopAsync();
            }

            await db.DisposeAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("❌ Error",
                $"No se pudo cargar la serie: {ex.Message}", "OK");
            await Navigation.PopAsync();
        }
    }

    private void Campo_Completed(object sender, EventArgs e)
    {
        OnGuardarClicked(sender, e);
    }

    private async void OnGuardarClicked(object sender, EventArgs e)
    {
        if (_isGuardando) return;

        try
        {
            _isGuardando = true;
            BtnGuardar.IsEnabled = false;
            BtnGuardar.Text = "💾 Guardando...";

            // Validaciones
            string modalidad = ModalidadEntry.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(modalidad))
            {
                await DisplayAlert("⚠️ Campo Requerido",
                    "Debe ingresar la modalidad de la serie (Ej: Bo3, Bo5, Bo7).", "OK");
                return;
            }

            if (modalidad.Length < 2)
            {
                await DisplayAlert("⚠️ Modalidad Inválida",
                    "La modalidad debe tener al menos 2 caracteres.", "OK");
                return;
            }

            var jugador1 = Jugador1Picker.SelectedItem as Jugador;
            var jugador2 = Jugador2Picker.SelectedItem as Jugador;

            if (jugador1 == null)
            {
                await DisplayAlert("⚠️ Jugador Requerido",
                    "Debe seleccionar el Jugador 1.", "OK");
                return;
            }

            if (jugador2 == null)
            {
                await DisplayAlert("⚠️ Jugador Requerido",
                    "Debe seleccionar el Jugador 2.", "OK");
                return;
            }

            if (jugador1.IdJugador == jugador2.IdJugador)
            {
                await DisplayAlert("⚠️ Jugadores Iguales",
                    "Los jugadores deben ser diferentes. Un jugador no puede enfrentarse a sí mismo.", "OK");
                return;
            }

            if (FechaPicker.Date > DateTime.Now)
            {
                bool confirmar = await DisplayAlert("⚠️ Fecha Futura",
                    "La fecha seleccionada es futura. ¿Desea continuar?",
                    "Sí", "No");

                if (!confirmar) return;
            }

            // Crear o actualizar
            var db = new AppDbContext();

            if (_serie == null || !_modoEdicion)
            {
                _serie = new Serie
                {
                    Modalidad = modalidad,
                    Fecha = FechaPicker.Date,
                    IdJugador1 = jugador1.IdJugador,
                    IdJugador2 = jugador2.IdJugador
                };
                db.Series.Add(_serie);
            }
            else
            {
                // Recargar la entidad en el nuevo contexto
                var serieExistente = await db.Series.FindAsync(_idSerieActual);
                if (serieExistente != null)
                {
                    serieExistente.Modalidad = modalidad;
                    serieExistente.Fecha = FechaPicker.Date;
                    serieExistente.IdJugador1 = jugador1.IdJugador;
                    serieExistente.IdJugador2 = jugador2.IdJugador;
                }
            }

            await db.SaveChangesAsync();
            await db.DisposeAsync();

            string mensaje = _modoEdicion
                ? $"Serie '{modalidad}' actualizada correctamente."
                : $"Serie '{modalidad}' creada correctamente.";

            await DisplayAlert("✅ Éxito", mensaje, "OK");
            await Navigation.PopAsync();
        }
        catch (DbUpdateException dbEx)
        {
            await DisplayAlert("❌ Error de Base de Datos",
                $"No se pudo guardar la serie: {dbEx.InnerException?.Message ?? dbEx.Message}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("❌ Error",
                $"Ocurrió un error inesperado: {ex.Message}", "OK");
        }
        finally
        {
            _isGuardando = false;
            BtnGuardar.IsEnabled = true;
            BtnGuardar.Text = "💾 GUARDAR SERIE";
        }
    }

    private async void OnEliminarClicked(object sender, EventArgs e)
    {
        try
        {
            if (_serie == null || !_idSerieActual.HasValue)
            {
                await DisplayAlert("❌ Error",
                    "No hay una serie para eliminar.", "OK");
                return;
            }

            var db = new AppDbContext();

            // Verificar si tiene juegos asociados
            bool tieneJuegos = await db.Juegos
                .AnyAsync(j => j.IdSerie == _idSerieActual.Value);

            string mensajeConfirmacion = tieneJuegos
                ? $"⚠️ Esta serie tiene juegos registrados.\n\n" +
                  $"Si la eliminas, se perderán todos los juegos asociados.\n\n" +
                  $"¿Estás seguro de eliminar la serie '{_serie.Modalidad}'?"
                : $"¿Estás seguro de eliminar la serie '{_serie.Modalidad}'?";

            bool confirmar = await DisplayAlert(
                "🗑️ Confirmar Eliminación",
                mensajeConfirmacion,
                "Sí, eliminar",
                "Cancelar");

            if (!confirmar)
            {
                await db.DisposeAsync();
                return;
            }

            BtnEliminar.IsEnabled = false;
            BtnEliminar.Text = "🗑️ Eliminando...";

            var serieEliminar = await db.Series.FindAsync(_idSerieActual.Value);
            if (serieEliminar != null)
            {
                db.Series.Remove(serieEliminar);
                await db.SaveChangesAsync();
            }

            await db.DisposeAsync();

            await DisplayAlert("✅ Eliminado",
                $"Serie '{_serie.Modalidad}' eliminada correctamente.", "OK");

            await Navigation.PopAsync();
        }
        catch (DbUpdateException)
        {
            await DisplayAlert("❌ No se puede eliminar",
                "Esta serie tiene datos relacionados y no puede ser eliminada. " +
                "Elimina primero los juegos asociados.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("❌ Error",
                $"No se pudo eliminar la serie: {ex.Message}", "OK");
        }
        finally
        {
            BtnEliminar.IsEnabled = true;
            BtnEliminar.Text = "🗑️ ELIMINAR SERIE";
        }
    }
}