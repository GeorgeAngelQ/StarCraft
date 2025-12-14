using StarCraft.Data;
using StarCraft.Models;
using Microsoft.EntityFrameworkCore;

namespace StarCraft.Views;

public partial class JugadoresPage : ContentPage
{
    private List<Jugador> jugadores = new();
    private List<Jugador> jugadoresFiltrados = new();
    private Jugador? jugadorEditando = null;
    private bool isGuardando = false;

    // Paginación
    private int paginaActual = 1;
    private int itemsPorPagina = 5;
    private int totalPaginas = 1;

    public JugadoresPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        CargarJugadores();
    }

    private async void CargarJugadores()
    {
        try
        {
            using var db = new AppDbContext();
            jugadores = await db.Jugadores.OrderBy(j => j.Alias).ToListAsync();
            jugadoresFiltrados = jugadores;

            paginaActual = 1;
            ActualizarPaginacion();
        }
        catch (Exception ex)
        {
            await DisplayAlert("❌ Error",
                $"No se pudo cargar la lista de jugadores: {ex.Message}", "OK");
        }
    }

    private void ActualizarPaginacion()
    {
        totalPaginas = (int)Math.Ceiling(jugadoresFiltrados.Count / (double)itemsPorPagina);

        if (totalPaginas == 0) totalPaginas = 1;
        if (paginaActual > totalPaginas) paginaActual = totalPaginas;

        var itemsPagina = jugadoresFiltrados
            .Skip((paginaActual - 1) * itemsPorPagina)
            .Take(itemsPorPagina)
            .ToList();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            JugadoresCollection.ItemsSource = itemsPagina;
            ActualizarControlesPaginacion();
        });
    }

    private void ActualizarControlesPaginacion()
    {
        LblPaginaActual.Text = $"Página {paginaActual} de {totalPaginas}";
        LblTotalRegistros.Text = $"Total: {jugadoresFiltrados.Count} jugador(es)";

        BtnPrimeraPagina.IsEnabled = paginaActual > 1;
        BtnAnterior.IsEnabled = paginaActual > 1;
        BtnSiguiente.IsEnabled = paginaActual < totalPaginas;
        BtnUltimaPagina.IsEnabled = paginaActual < totalPaginas;

        // Cambiar opacidad visual
        BtnPrimeraPagina.Opacity = BtnPrimeraPagina.IsEnabled ? 1.0 : 0.4;
        BtnAnterior.Opacity = BtnAnterior.IsEnabled ? 1.0 : 0.4;
        BtnSiguiente.Opacity = BtnSiguiente.IsEnabled ? 1.0 : 0.4;
        BtnUltimaPagina.Opacity = BtnUltimaPagina.IsEnabled ? 1.0 : 0.4;
    }

    private void Campo_Completed(object sender, EventArgs e)
    {
        OnGuardarClicked(sender, e);
    }

    private async void OnGuardarClicked(object sender, EventArgs e)
    {
        if (isGuardando) return;

        try
        {
            isGuardando = true;

            // Deshabilitar el botón mientras se guarda
            if (sender is Button button)
            {
                button.IsEnabled = false;
                button.Text = "💾 Guardando...";
            }

            var db = new AppDbContext();

            string alias = AliasEntry.Text?.Trim() ?? string.Empty;
            string pais = PaisEntry.Text?.Trim() ?? string.Empty;
            string? raza = string.IsNullOrWhiteSpace(RazaPicker.SelectedItem as string)
                ? null
                : RazaPicker.SelectedItem as string;

            // Validaciones
            if (string.IsNullOrWhiteSpace(alias))
            {
                await DisplayAlert("⚠️ Campo Requerido",
                    "El alias es obligatorio para registrar un jugador.", "OK");
                return;
            }

            if (alias.Length < 2)
            {
                await DisplayAlert("⚠️ Alias Inválido",
                    "El alias debe tener al menos 2 caracteres.", "OK");
                return;
            }

            // Verificar duplicados
            bool aliasExiste = await db.Jugadores
                .AnyAsync(j => j.Alias.ToLower() == alias.ToLower() &&
                               (jugadorEditando == null || j.IdJugador != jugadorEditando.IdJugador));

            if (aliasExiste)
            {
                await DisplayAlert("⚠️ Alias Duplicado",
                    $"Ya existe un jugador con el alias '{alias}'. Por favor, elige otro.", "OK");
                return;
            }

            // Guardar o actualizar
            if (jugadorEditando == null)
            {
                // Crear nuevo jugador
                var nuevo = new Jugador
                {
                    Alias = alias,
                    Pais = string.IsNullOrEmpty(pais) ? null : pais,
                    RazaPrincipal = raza
                };

                db.Jugadores.Add(nuevo);
                await db.SaveChangesAsync();

                await DisplayAlert("✅ Éxito",
                    $"Jugador '{alias}' registrado correctamente.", "OK");
            }
            else
            {
                // Actualizar jugador existente
                jugadorEditando.Alias = alias;
                jugadorEditando.Pais = string.IsNullOrEmpty(pais) ? null : pais;
                jugadorEditando.RazaPrincipal = raza;

                db.Jugadores.Update(jugadorEditando);
                await db.SaveChangesAsync();

                await DisplayAlert("✅ Actualizado",
                    $"Jugador '{alias}' actualizado correctamente.", "OK");

                jugadorEditando = null;
            }

            LimpiarFormulario();
            CargarJugadores();
        }
        catch (DbUpdateException dbEx)
        {
            await DisplayAlert("❌ Error de Base de Datos",
                $"No se pudo guardar el jugador: {dbEx.InnerException?.Message ?? dbEx.Message}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("❌ Error",
                $"Ocurrió un error inesperado: {ex.Message}", "OK");
        }
        finally
        {
            isGuardando = false;

            // Restaurar el botón
            if (sender is Button button)
            {
                button.IsEnabled = true;
                button.Text = "💾 GUARDAR JUGADOR";
            }
        }
    }

    private void LimpiarFormulario()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            AliasEntry.Text = "";
            PaisEntry.Text = "";
            RazaPicker.SelectedIndex = -1;
            jugadorEditando = null;

            // Enfocar el primer campo
            AliasEntry.Focus();
        });
    }

    private async void OnEditarClicked(object sender, EventArgs e)
    {
        try
        {
            var jugador = (sender as Button)?.CommandParameter as Jugador;
            if (jugador == null) return;

            jugadorEditando = jugador;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                AliasEntry.Text = jugador.Alias;
                PaisEntry.Text = jugador.Pais ?? "";

                if (!string.IsNullOrEmpty(jugador.RazaPrincipal))
                {
                    RazaPicker.SelectedItem = jugador.RazaPrincipal;
                }
                else
                {
                    RazaPicker.SelectedIndex = 0; // Vacío
                }

                // Scroll al formulario y enfocar
                AliasEntry.Focus();
            });

            await DisplayAlert("✏️ Modo Edición",
                $"Editando a '{jugador.Alias}'. Modifica los campos y presiona Guardar.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("❌ Error",
                $"No se pudo cargar el jugador para editar: {ex.Message}", "OK");
        }
    }

    private async void OnEliminarClicked(object sender, EventArgs e)
    {
        try
        {
            var jugador = (sender as Button)?.CommandParameter as Jugador;
            if (jugador == null) return;

            bool confirmar = await DisplayAlert(
                "🗑️ Confirmar Eliminación",
                $"¿Estás seguro de eliminar al jugador '{jugador.Alias}'?\n\n" +
                "⚠️ Esta acción no se puede deshacer y eliminará todos los datos relacionados.",
                "Sí, eliminar",
                "Cancelar");

            if (!confirmar) return;

            var db = new AppDbContext();

            // Verificar si el jugador tiene series o juegos asociados
            bool tieneRelaciones = await db.Series
                .AnyAsync(s => s.IdJugador1 == jugador.IdJugador || s.IdJugador2 == jugador.IdJugador);

            if (tieneRelaciones)
            {
                bool confirmarConDatos = await DisplayAlert(
                    "⚠️ Advertencia",
                    $"El jugador '{jugador.Alias}' tiene series y juegos asociados.\n\n" +
                    "Si lo eliminas, se perderán todos esos datos. ¿Deseas continuar?",
                    "Sí, eliminar todo",
                    "Cancelar");

                if (!confirmarConDatos)
                {
                    await db.DisposeAsync();
                    return;
                }
            }

            db.Jugadores.Remove(jugador);
            await db.SaveChangesAsync();
            await db.DisposeAsync();

            await DisplayAlert("✅ Eliminado",
                $"Jugador '{jugador.Alias}' eliminado correctamente.", "OK");

            // Limpiar formulario si estaba editando este jugador
            if (jugadorEditando?.IdJugador == jugador.IdJugador)
            {
                LimpiarFormulario();
            }

            CargarJugadores();
        }
        catch (DbUpdateException)
        {
            await DisplayAlert("❌ No se puede eliminar",
                "Este jugador tiene datos relacionados y no puede ser eliminado. " +
                "Elimina primero las series y juegos asociados.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("❌ Error",
                $"No se pudo eliminar el jugador: {ex.Message}", "OK");
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        try
        {
            string texto = e.NewTextValue?.ToLower() ?? "";

            jugadoresFiltrados = string.IsNullOrEmpty(texto)
                ? jugadores
                : jugadores.Where(j =>
                    j.Alias.ToLower().Contains(texto) ||
                    (j.Pais?.ToLower().Contains(texto) ?? false) ||
                    (j.RazaPrincipal?.ToLower().Contains(texto) ?? false)
                ).ToList();

            paginaActual = 1;
            ActualizarPaginacion();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error en búsqueda: {ex.Message}");
        }
    }

    private void OnJugadorSeleccionado(object sender, SelectionChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            JugadoresCollection.SelectedItem = null;
        });
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