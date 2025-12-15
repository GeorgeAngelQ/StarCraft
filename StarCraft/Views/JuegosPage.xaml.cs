using Microsoft.EntityFrameworkCore;
using StarCraft.Data;
using StarCraft.Models;

namespace StarCraft.Views;

public partial class JuegosPage : ContentPage
{
    private List<Serie> _series = new();
    private List<Serie> _seriesFiltradas = new();
    private List<Mapa> _mapas = new();
    private List<Jugador> _jugadores = new();
    private List<Juego> _juegos = new();
    private List<JuegoDisplay> _juegosFiltrados = new();

    private Juego? _editingJuego = null;
    private bool _isGuardando = false;
    private bool _isLoading = false;

    // Paginación
    private int paginaActual = 1;
    private int itemsPorPagina = 10;
    private int totalPaginas = 1;

    // Clase para display optimizado (sin dynamic)
    private class JuegoDisplay
    {
        public int IdJuego { get; set; }
        public string SerieText { get; set; } = string.Empty;
        public string MapaNombre { get; set; } = string.Empty;
        public string RazaJugador1 { get; set; } = string.Empty;
        public string RazaJugador2 { get; set; } = string.Empty;
        public string GanadorAlias { get; set; } = string.Empty;
    }

    // Clases auxiliares
    private class SeriesItem
    {
        public Serie Serie { get; set; } = null!;
        public string Display { get; set; } = string.Empty;
    }

    private class GenericItem<T>
    {
        public T Item { get; set; } = default!;
        public string Display { get; set; } = string.Empty;
    }

    public JuegosPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_isLoading)
        {
            await LoadReferencesAsync();
            await LoadJuegosAsync();
            SetFieldsEnabled(false);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Limpiar recursos al salir de la página
        LimpiarMemoria();
    }

    private void LimpiarMemoria()
    {
        _juegos?.Clear();
        _juegosFiltrados?.Clear();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    private async Task LoadReferencesAsync()
    {
        if (_isLoading) return;

        try
        {
            _isLoading = true;

            // Usar using para garantizar la liberación del DbContext
            await using var db = new AppDbContext();

            // Cargar series ordenadas por fecha descendente (más recientes primero)
            _series = await db.Series
                .Include(s => s.Jugador1)
                .Include(s => s.Jugador2)
                .OrderByDescending(s => s.Fecha)
                .ThenByDescending(s => s.IdSerie)
                .AsNoTracking() // ✅ No trackear cambios = menos memoria
                .ToListAsync();

            _mapas = await db.Mapas
                .OrderBy(m => m.Nombre)
                .AsNoTracking()
                .ToListAsync();

            _jugadores = await db.Jugadores
                .OrderBy(j => j.Alias)
                .AsNoTracking()
                .ToListAsync();

            // Obtener modalidades únicas
            var modalidades = _series
                .Select(s => s.Modalidad)
                .Distinct()
                .OrderBy(m => m)
                .ToList();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Configurar picker de modalidades
                ModalidadPicker.ItemsSource = modalidades;
                ModalidadPicker.SelectedIndex = -1;

                // Configurar picker de mapas
                var mapaItems = _mapas.Select(m => new GenericItem<Mapa> { Item = m, Display = m.Nombre }).ToList();
                MapaPicker.ItemsSource = mapaItems;
                MapaPicker.ItemDisplayBinding = new Binding("Display");

                // Configurar pickers de razas
                var races = new List<string?> { null, "Terran", "Zerg", "Protoss" };
                Raza1Picker.ItemsSource = races;
                Raza2Picker.ItemsSource = races;

                // Inicialmente, no hay series para mostrar
                SeriePicker.ItemsSource = null;
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("❌ Error",
                $"No se pudieron cargar las referencias: {ex.Message}", "OK");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ModalidadPicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (ModalidadPicker.SelectedIndex == -1)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SeriePicker.ItemsSource = null;
                SeriePicker.SelectedIndex = -1;
                SetFieldsEnabled(false);
            });
            _seriesFiltradas = new List<Serie>();
            _juegos = new List<Juego>();
            _juegosFiltrados = new List<JuegoDisplay>();
            ActualizarPaginacion();
            return;
        }

        var modalidadSeleccionada = ModalidadPicker.SelectedItem as string;

        // Filtrar series por modalidad (ya están ordenadas por fecha descendente)
        _seriesFiltradas = _series
            .Where(s => s.Modalidad == modalidadSeleccionada)
            .ToList();

        var seriesItems = _seriesFiltradas.Select(s => new SeriesItem
        {
            Serie = s,
            Display = $"{s.Jugador1?.Alias} vs {s.Jugador2?.Alias} ({s.Fecha:dd/MM/yyyy})"
        }).ToList();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            SeriePicker.ItemsSource = seriesItems;
            SeriePicker.ItemDisplayBinding = new Binding("Display");
            SeriePicker.SelectedIndex = -1;
            GanadorPicker.ItemsSource = null;
            GanadorPicker.SelectedIndex = -1;
            MapaPicker.SelectedIndex = -1;
            Raza1Picker.SelectedIndex = -1;
            Raza2Picker.SelectedIndex = -1;
            SetFieldsEnabled(false);
        });

        // Limpiar lista de juegos
        _juegos?.Clear();
        _juegosFiltrados?.Clear();
        ActualizarPaginacion();
    }

    private async Task LoadJuegosAsync()
    {
        if (_isLoading) return;

        try
        {
            _isLoading = true;

            if (SeriePicker.SelectedItem is not SeriesItem selectedItem)
            {
                _juegos = new List<Juego>();
                _juegosFiltrados = new List<JuegoDisplay>();
                ActualizarPaginacion();
                return;
            }

            await using var db = new AppDbContext();

            // Cargar solo los datos necesarios
            var juegosData = await db.Juegos
                .Where(j => j.IdSerie == selectedItem.Serie.IdSerie)
                .OrderByDescending(j => j.FechaCreacion)
                .AsNoTracking()
                .Select(j => new
                {
                    j.IdJuego,
                    j.IdSerie,
                    j.IdMapa,
                    j.IdGanador,
                    j.RazaJugador1,
                    j.RazaJugador2,
                    j.FechaCreacion,
                    SerieModalidad = j.Serie!.Modalidad,
                    SerieJugador1Alias = j.Serie.Jugador1!.Alias,
                    SerieJugador2Alias = j.Serie.Jugador2!.Alias,
                    SerieFecha = j.Serie.Fecha,
                    MapaNombre = j.Mapa!.Nombre,
                    GanadorAlias = j.Ganador!.Alias
                })
                .ToListAsync();

            // Convertir a objetos Juego para edición
            _juegos = juegosData.Select(j => new Juego
            {
                IdJuego = j.IdJuego,
                IdSerie = j.IdSerie,
                IdMapa = j.IdMapa,
                IdGanador = j.IdGanador,
                RazaJugador1 = j.RazaJugador1,
                RazaJugador2 = j.RazaJugador2,
                FechaCreacion = j.FechaCreacion
            }).ToList();

            // Crear objetos de display (NO dynamic)
            _juegosFiltrados = juegosData.Select(j => new JuegoDisplay
            {
                IdJuego = j.IdJuego,
                SerieText = $"{j.SerieModalidad} - {j.SerieJugador1Alias} vs {j.SerieJugador2Alias} ({j.SerieFecha:dd/MM/yyyy})",
                MapaNombre = j.MapaNombre ?? "Sin mapa",
                RazaJugador1 = j.RazaJugador1 ?? "N/A",
                RazaJugador2 = j.RazaJugador2 ?? "N/A",
                GanadorAlias = j.GanadorAlias ?? "N/A"
            }).ToList();

            paginaActual = 1;
            ActualizarPaginacion();
        }
        catch (Exception ex)
        {
            await DisplayAlert("❌ Error",
                $"No se pudieron cargar los juegos: {ex.Message}", "OK");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ActualizarPaginacion()
    {
        totalPaginas = (int)Math.Ceiling(_juegosFiltrados.Count / (double)itemsPorPagina);

        if (totalPaginas == 0) totalPaginas = 1;
        if (paginaActual > totalPaginas) paginaActual = totalPaginas;

        var itemsPagina = _juegosFiltrados
            .Skip((paginaActual - 1) * itemsPorPagina)
            .Take(itemsPorPagina)
            .ToList();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            JuegosCollection.ItemsSource = null; // ✅ Limpiar primero
            JuegosCollection.ItemsSource = itemsPagina;
            ActualizarControlesPaginacion();
        });
    }

    private void ActualizarControlesPaginacion()
    {
        LblPaginaActual.Text = $"Página {paginaActual} de {totalPaginas}";
        LblTotalRegistros.Text = $"Total: {_juegosFiltrados.Count} juego(s)";

        BtnPrimeraPagina.IsEnabled = paginaActual > 1;
        BtnAnterior.IsEnabled = paginaActual > 1;
        BtnSiguiente.IsEnabled = paginaActual < totalPaginas;
        BtnUltimaPagina.IsEnabled = paginaActual < totalPaginas;

        BtnPrimeraPagina.Opacity = BtnPrimeraPagina.IsEnabled ? 1.0 : 0.4;
        BtnAnterior.Opacity = BtnAnterior.IsEnabled ? 1.0 : 0.4;
        BtnSiguiente.Opacity = BtnSiguiente.IsEnabled ? 1.0 : 0.4;
        BtnUltimaPagina.Opacity = BtnUltimaPagina.IsEnabled ? 1.0 : 0.4;
    }

    private void SetFieldsEnabled(bool enabled)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            MapaPicker.IsEnabled = enabled;
            GanadorPicker.IsEnabled = enabled;
            Raza1Picker.IsEnabled = enabled;
            Raza2Picker.IsEnabled = enabled;
        });
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
        if (serie.Jugador1 != null)
            winners.Add(new GenericItem<Jugador> { Item = serie.Jugador1, Display = serie.Jugador1.Alias });
        if (serie.Jugador2 != null)
            winners.Add(new GenericItem<Jugador> { Item = serie.Jugador2, Display = serie.Jugador2.Alias });

        MainThread.BeginInvokeOnMainThread(() =>
        {
            GanadorPicker.ItemsSource = winners;
            GanadorPicker.ItemDisplayBinding = new Binding("Display");
            GanadorPicker.SelectedIndex = -1;
            MapaPicker.SelectedIndex = -1;
            Raza1Picker.SelectedIndex = -1;
            Raza2Picker.SelectedIndex = -1;
        });

        await LoadJuegosAsync();
    }

    private async void GuardarButton_Clicked(object sender, EventArgs e)
    {
        if (_isGuardando || _isLoading) return;

        try
        {
            _isGuardando = true;
            BtnGuardar.IsEnabled = false;
            BtnGuardar.Text = "💾 Guardando...";

            // Validaciones
            if (ModalidadPicker.SelectedIndex == -1)
            {
                await DisplayAlert("⚠️ Campo Requerido", "Selecciona una modalidad primero.", "OK");
                return;
            }
            if (SeriePicker.SelectedIndex == -1)
            {
                await DisplayAlert("⚠️ Campo Requerido", "Selecciona una serie.", "OK");
                return;
            }
            if (MapaPicker.SelectedIndex == -1)
            {
                await DisplayAlert("⚠️ Campo Requerido", "Selecciona un mapa.", "OK");
                return;
            }
            if (GanadorPicker.SelectedIndex == -1)
            {
                await DisplayAlert("⚠️ Campo Requerido",
                    "Selecciona el ganador (uno de los dos jugadores de la serie).", "OK");
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

            await using var db = new AppDbContext();

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

                db.Juegos.Add(nuevo);
                await db.SaveChangesAsync();
                await DisplayAlert("✅ Éxito", "Juego registrado correctamente.", "OK");
            }
            else
            {
                var juegoExistente = await db.Juegos.FindAsync(_editingJuego.IdJuego);
                if (juegoExistente != null)
                {
                    juegoExistente.IdSerie = selectedSerie.IdSerie;
                    juegoExistente.IdMapa = selectedMapa.IdMapa;
                    juegoExistente.RazaJugador1 = raza1;
                    juegoExistente.RazaJugador2 = raza2;
                    juegoExistente.IdGanador = selectedGanador.IdJugador;

                    db.Juegos.Update(juegoExistente);
                    await db.SaveChangesAsync();
                    await DisplayAlert("✅ Actualizado", "Juego actualizado correctamente.", "OK");
                }

                _editingJuego = null;
            }

            await LoadJuegosAsync();
            NuevoButton_Clicked(null, EventArgs.Empty);
        }
        catch (DbUpdateException dbEx)
        {
            await DisplayAlert("❌ Error de Base de Datos",
                $"No se pudo guardar el juego: {dbEx.InnerException?.Message ?? dbEx.Message}", "OK");
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
            BtnGuardar.Text = "💾 GUARDAR";
        }
    }

    private void NuevoButton_Clicked(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ModalidadPicker.SelectedIndex = -1;
            SeriePicker.ItemsSource = null;
            SeriePicker.SelectedIndex = -1;
            MapaPicker.SelectedIndex = -1;
            GanadorPicker.ItemsSource = null;
            GanadorPicker.SelectedIndex = -1;
            Raza1Picker.SelectedIndex = -1;
            Raza2Picker.SelectedIndex = -1;
            SetFieldsEnabled(false);
            _editingJuego = null;
        });
    }

    private async void EditarButton_Clicked(object sender, EventArgs e)
    {
        if (_isLoading) return;

        try
        {
            if (!(sender is Button btn) || !(btn.CommandParameter is int id)) return;

            await using var db = new AppDbContext();

            var juego = await db.Juegos
                .Include(j => j.Serie).ThenInclude(s => s!.Jugador1)
                .Include(j => j.Serie).ThenInclude(s => s!.Jugador2)
                .Include(j => j.Mapa)
                .Include(j => j.Ganador)
                .AsNoTracking()
                .FirstOrDefaultAsync(j => j.IdJuego == id);

            if (juego == null)
            {
                await DisplayAlert("❌ Error", "No se encontró el juego especificado.", "OK");
                return;
            }

            _editingJuego = juego;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Seleccionar modalidad primero
                var modalidadIndex = ((List<string>)ModalidadPicker.ItemsSource!).IndexOf(juego.Serie!.Modalidad!);
                if (modalidadIndex >= 0)
                {
                    ModalidadPicker.SelectedIndex = modalidadIndex;
                }

                // Después de que se filtre la serie, seleccionar la serie
                var seriesItems = (List<SeriesItem>)SeriePicker.ItemsSource!;
                var indexSerie = seriesItems.FindIndex(si => si.Serie.IdSerie == juego.IdSerie);
                if (indexSerie >= 0) SeriePicker.SelectedIndex = indexSerie;

                var mapaItems = (List<GenericItem<Mapa>>)MapaPicker.ItemsSource!;
                var indexMapa = mapaItems.FindIndex(mi => mi.Item.IdMapa == juego.IdMapa);
                if (indexMapa >= 0) MapaPicker.SelectedIndex = indexMapa;

                if (GanadorPicker.ItemsSource is List<GenericItem<Jugador>> winners)
                {
                    var idxWin = winners.FindIndex(w => w.Item.IdJugador == juego.IdGanador);
                    if (idxWin >= 0) GanadorPicker.SelectedIndex = idxWin;
                }

                Raza1Picker.SelectedItem = juego.RazaJugador1;
                Raza2Picker.SelectedItem = juego.RazaJugador2;
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("❌ Error",
                $"No se pudo cargar el juego para editar: {ex.Message}", "OK");
        }
    }

    private async void EliminarButton_Clicked(object sender, EventArgs e)
    {
        if (_isLoading) return;

        try
        {
            if (!(sender is Button btn) || !(btn.CommandParameter is int id)) return;

            var confirm = await DisplayAlert("🗑️ Confirmar Eliminación",
                "¿Estás seguro de eliminar este juego?",
                "Sí, eliminar",
                "Cancelar");

            if (!confirm) return;

            btn.IsEnabled = false;
            btn.Text = "🗑️";

            await using var db = new AppDbContext();
            var juego = await db.Juegos.FindAsync(id);

            if (juego == null)
            {
                await DisplayAlert("❌ Error", "No se encontró el juego especificado.", "OK");
                return;
            }

            db.Juegos.Remove(juego);
            await db.SaveChangesAsync();

            await DisplayAlert("✅ Eliminado", "Juego eliminado correctamente.", "OK");
            await LoadJuegosAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("❌ Error",
                $"No se pudo eliminar el juego: {ex.Message}", "OK");
        }
    }

    private void SearchBar_TextChanged(object sender, TextChangedEventArgs e)
    {
        try
        {
            var q = e.NewTextValue?.Trim().ToLower() ?? "";

            if (string.IsNullOrWhiteSpace(q))
            {
                _juegosFiltrados = _juegos.Select(j => new JuegoDisplay
                {
                    IdJuego = j.IdJuego,
                    SerieText = $"{j.Serie?.Modalidad} - {j.Serie?.Jugador1?.Alias} vs {j.Serie?.Jugador2?.Alias} ({j.Serie?.Fecha:dd/MM/yyyy})",
                    MapaNombre = j.Mapa?.Nombre ?? "Sin mapa",
                    RazaJugador1 = j.RazaJugador1 ?? "N/A",
                    RazaJugador2 = j.RazaJugador2 ?? "N/A",
                    GanadorAlias = j.Ganador?.Alias ?? "N/A"
                }).ToList();
            }
            else
            {
                _juegosFiltrados = _juegos.Where(j =>
                    (j.Serie?.Modalidad?.ToLower().Contains(q) ?? false) ||
                    (j.Mapa?.Nombre?.ToLower().Contains(q) ?? false) ||
                    (j.RazaJugador1?.ToLower().Contains(q) ?? false) ||
                    (j.RazaJugador2?.ToLower().Contains(q) ?? false) ||
                    (j.Ganador?.Alias?.ToLower().Contains(q) ?? false) ||
                    (j.Serie?.Jugador1?.Alias?.ToLower().Contains(q) ?? false) ||
                    (j.Serie?.Jugador2?.Alias?.ToLower().Contains(q) ?? false)
                ).Select(j => new JuegoDisplay
                {
                    IdJuego = j.IdJuego,
                    SerieText = $"{j.Serie?.Modalidad} - {j.Serie?.Jugador1?.Alias} vs {j.Serie?.Jugador2?.Alias} ({j.Serie?.Fecha:dd/MM/yyyy})",
                    MapaNombre = j.Mapa?.Nombre ?? "Sin mapa",
                    RazaJugador1 = j.RazaJugador1 ?? "N/A",
                    RazaJugador2 = j.RazaJugador2 ?? "N/A",
                    GanadorAlias = j.Ganador?.Alias ?? "N/A"
                }).ToList();
            }

            paginaActual = 1;
            ActualizarPaginacion();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error en búsqueda: {ex.Message}");
        }
    }

    private void JuegosCollection_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ((CollectionView)sender).SelectedItem = null;
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