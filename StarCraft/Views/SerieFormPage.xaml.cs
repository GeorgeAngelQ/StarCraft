using StarCraft.Data;
using StarCraft.Models;
using Microsoft.EntityFrameworkCore;

namespace StarCraft.Views;

public partial class SerieFormPage : ContentPage
{
    private Serie? _serie;
    private bool _modoEdicion = false;
    private bool _isGuardando = false;
    private bool _isLoading = false;
    private int? _idSerieActual = null;

    // Modalidades de la BD
    private List<string> _modalidadesExistentes = new();

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

        CargarModalidadesExistentes();
        CargarJugadores();

        if (idSerie.HasValue)
            CargarSerie(idSerie.Value);
    }

    private async void CargarModalidadesExistentes()
    {
        try
        {
            await using var db = new AppDbContext();

            // Obtener modalidades únicas de la BD, ordenadas alfabéticamente
            var modalidades = await db.Series
                .Select(s => s.Modalidad)
                .Distinct()
                .OrderBy(m => m)
                .AsNoTracking()
                .ToListAsync();

            _modalidadesExistentes = modalidades;

            // Agregar opción para nueva modalidad al final
            _modalidadesExistentes.Add("➕ Nueva modalidad...");

            MainThread.BeginInvokeOnMainThread(() =>
            {
                ModalidadPicker.ItemsSource = _modalidadesExistentes;
                ModalidadPicker.SelectedIndex = -1;
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al cargar modalidades: {ex.Message}");
            // Si hay error, al menos tener la opción de nueva modalidad
            _modalidadesExistentes = new List<string> { "➕ Nueva modalidad..." };
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ModalidadPicker.ItemsSource = _modalidadesExistentes;
                ModalidadPicker.SelectedIndex = -1;
            });
        }
    }

    private async void CargarJugadores()
    {
        if (_isLoading) return;

        try
        {
            _isLoading = true;

            await using var db = new AppDbContext();
            var jugadores = await db.Jugadores
                .OrderBy(j => j.Alias)
                .AsNoTracking()
                .ToListAsync();

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
        finally
        {
            _isLoading = false;
        }
    }

    private async void CargarSerie(int idSerie)
    {
        if (_isLoading) return;

        try
        {
            _isLoading = true;

            await using var db = new AppDbContext();
            _serie = await db.Series
                .Include(s => s.Jugador1)
                .Include(s => s.Jugador2)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.IdSerie == idSerie);

            if (_serie != null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    FechaPicker.Date = _serie.Fecha;

                    // Verificar si la modalidad está en las existentes
                    int indexModalidad = _modalidadesExistentes.IndexOf(_serie.Modalidad);

                    if (indexModalidad >= 0 && indexModalidad < _modalidadesExistentes.Count - 1)
                    {
                        // Es una modalidad existente en la BD
                        ModalidadPicker.SelectedIndex = indexModalidad;
                        ModalidadEntry.Text = string.Empty;
                        ModalidadEntry.IsEnabled = false;
                    }
                    else
                    {
                        // Es una modalidad nueva o no encontrada
                        ModalidadPicker.SelectedIndex = _modalidadesExistentes.Count - 1; // "➕ Nueva modalidad..."
                        ModalidadEntry.Text = _serie.Modalidad;
                        ModalidadEntry.IsEnabled = true;
                    }
                });

                var jugador1 = await db.Jugadores
                    .AsNoTracking()
                    .FirstOrDefaultAsync(j => j.IdJugador == _serie.IdJugador1);
                var jugador2 = await db.Jugadores
                    .AsNoTracking()
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
        }
        catch (Exception ex)
        {
            await DisplayAlert("❌ Error",
                $"No se pudo cargar la serie: {ex.Message}", "OK");
            await Navigation.PopAsync();
        }
        finally
        {
            _isLoading = false;
        }
    }

    // Evento cuando se selecciona una modalidad del Picker
    private void ModalidadPicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (ModalidadPicker.SelectedIndex == -1)
        {
            ModalidadEntry.IsEnabled = true;
            return;
        }

        string seleccion = ModalidadPicker.SelectedItem as string ?? string.Empty;

        if (seleccion == "➕ Nueva modalidad...")
        {
            // Activar el Entry para que el usuario escriba una nueva
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ModalidadEntry.IsEnabled = true;
                ModalidadEntry.Text = string.Empty;
                ModalidadEntry.Focus();
            });
        }
        else
        {
            // Desactivar el Entry y usar la modalidad del Picker
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ModalidadEntry.Text = string.Empty;
                ModalidadEntry.IsEnabled = false;
            });
        }
    }

    // Evento cuando el usuario empieza a escribir en el Entry
    private void ModalidadEntry_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(e.NewTextValue))
        {
            // Si el usuario escribe, desactivar el Picker (prioridad al Entry)
            if (ModalidadPicker.SelectedIndex != _modalidadesExistentes.Count - 1)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ModalidadPicker.SelectedIndex = -1;
                });
            }
        }
    }

    // Evento cuando el Entry pierde el foco
    private void ModalidadEntry_Unfocused(object sender, FocusEventArgs e)
    {
        // Si el Entry está vacío, reactivar el Picker
        if (string.IsNullOrWhiteSpace(ModalidadEntry.Text))
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ModalidadEntry.IsEnabled = true;
            });
        }
    }

    private string ObtenerModalidadSeleccionada()
    {
        // Prioridad 1: Si hay texto en el Entry, usarlo
        if (!string.IsNullOrWhiteSpace(ModalidadEntry.Text))
        {
            return ModalidadEntry.Text.Trim();
        }

        // Prioridad 2: Si hay una selección en el Picker (que no sea "➕ Nueva modalidad...")
        if (ModalidadPicker.SelectedIndex >= 0 &&
            ModalidadPicker.SelectedIndex < _modalidadesExistentes.Count - 1)
        {
            return ModalidadPicker.SelectedItem as string ?? string.Empty;
        }

        return string.Empty;
    }

    private void Campo_Completed(object sender, EventArgs e)
    {
        OnGuardarClicked(sender, e);
    }

    private async void OnGuardarClicked(object sender, EventArgs e)
    {
        if (_isGuardando || _isLoading) return;

        try
        {
            _isGuardando = true;
            BtnGuardar.IsEnabled = false;
            BtnGuardar.Text = "💾 Guardando...";

            // Obtener modalidad (Entry tiene prioridad sobre Picker)
            string modalidad = ObtenerModalidadSeleccionada();

            if (string.IsNullOrWhiteSpace(modalidad))
            {
                await DisplayAlert("⚠️ Campo Requerido",
                    "Debe seleccionar una modalidad del menú o escribir una personalizada.", "OK");
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
            await using var db = new AppDbContext();

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
        if (_isLoading) return;

        try
        {
            if (_serie == null || !_idSerieActual.HasValue)
            {
                await DisplayAlert("❌ Error",
                    "No hay una serie para eliminar.", "OK");
                return;
            }

            await using var db = new AppDbContext();

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