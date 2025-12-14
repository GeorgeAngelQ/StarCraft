using Microsoft.EntityFrameworkCore;
using StarCraft.Data;
using StarCraft.Models;

namespace StarCraft.Views
{
    public partial class MapasPage : ContentPage
    {
        private List<Mapa> mapas = new();
        private List<Mapa> mapasFiltrados = new();
        private bool isGuardando = false;

        // Paginación
        private int paginaActual = 1;
        private int itemsPorPagina = 15;
        private int totalPaginas = 1;

        public MapasPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await CargarMapas();
        }

        private async Task CargarMapas()
        {
            try
            {
                var db = new AppDbContext();
                mapas = await db.Mapas
                    .OrderBy(m => m.Nombre)
                    .ToListAsync();
                await db.DisposeAsync();

                mapasFiltrados = mapas;
                paginaActual = 1;
                ActualizarPaginacion();
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ Error",
                    $"No se pudo cargar la lista de mapas: {ex.Message}", "OK");
            }
        }

        private void ActualizarPaginacion()
        {
            totalPaginas = (int)Math.Ceiling(mapasFiltrados.Count / (double)itemsPorPagina);

            if (totalPaginas == 0) totalPaginas = 1;
            if (paginaActual > totalPaginas) paginaActual = totalPaginas;

            var itemsPagina = mapasFiltrados
                .Skip((paginaActual - 1) * itemsPorPagina)
                .Take(itemsPorPagina)
                .ToList();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                MapasCollection.ItemsSource = itemsPagina;
                ActualizarControlesPaginacion();
            });
        }

        private void ActualizarControlesPaginacion()
        {
            LblPaginaActual.Text = $"Página {paginaActual} de {totalPaginas}";
            LblTotalRegistros.Text = $"Total: {mapasFiltrados.Count} mapa(s)";

            BtnPrimeraPagina.IsEnabled = paginaActual > 1;
            BtnAnterior.IsEnabled = paginaActual > 1;
            BtnSiguiente.IsEnabled = paginaActual < totalPaginas;
            BtnUltimaPagina.IsEnabled = paginaActual < totalPaginas;

            BtnPrimeraPagina.Opacity = BtnPrimeraPagina.IsEnabled ? 1.0 : 0.4;
            BtnAnterior.Opacity = BtnAnterior.IsEnabled ? 1.0 : 0.4;
            BtnSiguiente.Opacity = BtnSiguiente.IsEnabled ? 1.0 : 0.4;
            BtnUltimaPagina.Opacity = BtnUltimaPagina.IsEnabled ? 1.0 : 0.4;
        }

        private void Campo_Completed(object sender, EventArgs e)
        {
            OnRegistrarClicked(sender, e);
        }

        private async void OnRegistrarClicked(object sender, EventArgs e)
        {
            if (isGuardando) return;

            try
            {
                isGuardando = true;
                BtnRegistrar.IsEnabled = false;
                BtnRegistrar.Text = "💾 Guardando...";

                var nombre = NombreEntry.Text?.Trim();

                if (string.IsNullOrWhiteSpace(nombre))
                {
                    await DisplayAlert("⚠️ Campo Requerido",
                        "Por favor, ingresa el nombre del mapa.", "OK");
                    return;
                }

                if (nombre.Length < 3)
                {
                    await DisplayAlert("⚠️ Nombre Inválido",
                        "El nombre del mapa debe tener al menos 3 caracteres.", "OK");
                    return;
                }

                var db = new AppDbContext();
                bool existe = await db.Mapas.AnyAsync(m => m.Nombre.ToLower() == nombre.ToLower());

                if (existe)
                {
                    await db.DisposeAsync();
                    await DisplayAlert("⚠️ Mapa Duplicado",
                        $"El mapa '{nombre}' ya existe en el sistema.", "OK");
                    return;
                }

                var mapa = new Mapa { Nombre = nombre };
                db.Mapas.Add(mapa);
                await db.SaveChangesAsync();
                await db.DisposeAsync();

                await DisplayAlert("✅ Éxito",
                    $"Mapa '{nombre}' registrado correctamente.", "OK");

                NombreEntry.Text = string.Empty;
                await CargarMapas();

                // Enfocar el campo para siguiente entrada
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    NombreEntry.Focus();
                });
            }
            catch (DbUpdateException dbEx)
            {
                await DisplayAlert("❌ Error de Base de Datos",
                    $"No se pudo guardar el mapa: {dbEx.InnerException?.Message ?? dbEx.Message}", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ Error",
                    $"Ocurrió un error inesperado: {ex.Message}", "OK");
            }
            finally
            {
                isGuardando = false;
                BtnRegistrar.IsEnabled = true;
                BtnRegistrar.Text = "💾 GUARDAR";
            }
        }

        private void OnBuscarMapa(object sender, TextChangedEventArgs e)
        {
            try
            {
                var texto = e.NewTextValue?.ToLower() ?? "";

                mapasFiltrados = string.IsNullOrEmpty(texto)
                    ? mapas
                    : mapas.Where(m => m.Nombre.ToLower().Contains(texto)).ToList();

                paginaActual = 1;
                ActualizarPaginacion();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en búsqueda: {ex.Message}");
            }
        }

        private async void OnEliminarClicked(object sender, EventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.CommandParameter is int id)
                {
                    var db = new AppDbContext();
                    var mapa = await db.Mapas.FindAsync(id);

                    if (mapa == null)
                    {
                        await db.DisposeAsync();
                        await DisplayAlert("❌ Error",
                            "No se encontró el mapa especificado.", "OK");
                        return;
                    }

                    // Verificar si tiene juegos asociados
                    bool tieneJuegos = await db.Juegos.AnyAsync(j => j.IdMapa == id);

                    string mensajeConfirmacion = tieneJuegos
                        ? $"⚠️ El mapa '{mapa.Nombre}' tiene juegos registrados.\n\n" +
                          "Si lo eliminas, se perderán todos los juegos asociados.\n\n" +
                          "¿Deseas continuar?"
                        : $"¿Estás seguro de eliminar el mapa '{mapa.Nombre}'?";

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

                    btn.IsEnabled = false;
                    btn.Text = "🗑️ Eliminando...";

                    db.Mapas.Remove(mapa);
                    await db.SaveChangesAsync();
                    await db.DisposeAsync();

                    await DisplayAlert("✅ Eliminado",
                        $"Mapa '{mapa.Nombre}' eliminado correctamente.", "OK");

                    await CargarMapas();
                }
            }
            catch (DbUpdateException)
            {
                await DisplayAlert("❌ No se puede eliminar",
                    "Este mapa tiene juegos asociados y no puede ser eliminado. " +
                    "Elimina primero los juegos relacionados.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ Error",
                    $"No se pudo eliminar el mapa: {ex.Message}", "OK");
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
}