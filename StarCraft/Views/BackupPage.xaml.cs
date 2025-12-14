using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using StarCraft.Data;
using StarCraft.Models;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace StarCraft.Views;

public partial class BackupPage : ContentPage
{
    private const string PREF_ULTIMO_BACKUP = "ultimo_backup";

    public BackupPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        CargarEstadisticas();
        CargarFechaUltimoBackup();
    }

    private async void CargarEstadisticas()
    {
        try
        {
            var db = new AppDbContext();

            var jugadores = await db.Jugadores.CountAsync();
            var mapas = await db.Mapas.CountAsync();
            var series = await db.Series.CountAsync();
            var juegos = await db.Juegos.CountAsync();

            await db.DisposeAsync();

            var dbInfo = AppDbContext.GetDatabaseInfo();
            var lines = dbInfo.Split('\n');
            var tamano = lines.FirstOrDefault(l => l.Contains("Tamaño:"))?.Split(':').LastOrDefault()?.Trim() ?? "0 KB";

            LblEstadisticas.Text = $@"📊 Datos actuales:
• Jugadores: {jugadores}
• Mapas: {mapas}
• Series: {series}
• Juegos: {juegos}

💾 Tamaño de BD: {tamano}";
        }
        catch (Exception ex)
        {
            LblEstadisticas.Text = $"❌ Error al cargar estadísticas:\n{ex.Message}";
        }
    }

    private void CargarFechaUltimoBackup()
    {
        var ultimoBackup = Preferences.Get(PREF_ULTIMO_BACKUP, string.Empty);
        if (!string.IsNullOrEmpty(ultimoBackup))
        {
            if (DateTime.TryParse(ultimoBackup, out DateTime fecha))
            {
                LblUltimoBackup.Text = $"Último respaldo: {fecha:dd/MM/yyyy HH:mm}";
            }
        }
    }

    private void GuardarFechaBackup()
    {
        Preferences.Set(PREF_ULTIMO_BACKUP, DateTime.Now.ToString("o"));
        CargarFechaUltimoBackup();
    }

    // ==================== EXPORTAR ====================

    private async void OnExportarSQLiteClicked(object sender, EventArgs e)
    {
        try
        {
            var button = sender as Button;
            button.IsEnabled = false;
            button.Text = "📁 Exportando...";

            var dbInfo = AppDbContext.GetDatabaseInfo();
            var lines = dbInfo.Split('\n');
            var rutaOrigen = lines.FirstOrDefault(l => l.StartsWith("Ruta:"))?.Replace("Ruta:", "").Trim();

            if (string.IsNullOrEmpty(rutaOrigen) || !File.Exists(rutaOrigen))
            {
                await DisplayAlert("❌ Error", "No se encontró la base de datos.", "OK");
                return;
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var nombreArchivo = $"starcraft_backup_{timestamp}.db";
            var rutaDestino = Path.Combine(FileSystem.AppDataDirectory, nombreArchivo);

            // Copiar archivo SQLite
            File.Copy(rutaOrigen, rutaDestino, true);

            // Compartir archivo
            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Exportar Base de Datos",
                File = new ShareFile(rutaDestino)
            });

            GuardarFechaBackup();
            await DisplayAlert("✅ Éxito",
                $"Base de datos exportada correctamente.\n\nArchivo: {nombreArchivo}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("❌ Error", $"No se pudo exportar:\n{ex.Message}", "OK");
        }
        finally
        {
            var button = sender as Button;
            button.IsEnabled = true;
            button.Text = "📁 SQLite (.db)";
        }
    }

    private async void OnExportarJSONClicked(object sender, EventArgs e)
    {
        try
        {
            var button = sender as Button;
            button.IsEnabled = false;
            button.Text = "📋 Exportando...";

            var db = new AppDbContext();

            var data = new
            {
                FechaExportacion = DateTime.Now,
                Version = "1.0",
                Jugadores = await db.Jugadores.ToListAsync(),
                Mapas = await db.Mapas.ToListAsync(),
                Series = await db.Series.ToListAsync(),
                Juegos = await db.Juegos.ToListAsync()
            };

            await db.DisposeAsync();

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(data, options);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var nombreArchivo = $"starcraft_backup_{timestamp}.json";
            var rutaArchivo = Path.Combine(FileSystem.AppDataDirectory, nombreArchivo);

            await File.WriteAllTextAsync(rutaArchivo, json, Encoding.UTF8);

            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Exportar a JSON",
                File = new ShareFile(rutaArchivo)
            });

            GuardarFechaBackup();
            await DisplayAlert("✅ Éxito",
                $"Datos exportados a JSON.\n\nArchivo: {nombreArchivo}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("❌ Error", $"No se pudo exportar:\n{ex.Message}", "OK");
        }
        finally
        {
            var button = sender as Button;
            button.IsEnabled = true;
            button.Text = "📋 JSON";
        }
    }

    private async void OnExportarCSVClicked(object sender, EventArgs e)
    {
        try
        {
            var button = sender as Button;
            button.IsEnabled = false;
            button.Text = "📊 Exportando...";

            var db = new AppDbContext();

            var jugadores = await db.Jugadores.ToListAsync();
            var mapas = await db.Mapas.ToListAsync();
            var series = await db.Series
                .Include(s => s.Jugador1)
                .Include(s => s.Jugador2)
                .ToListAsync();
            var juegos = await db.Juegos
                .Include(j => j.Serie)
                .Include(j => j.Mapa)
                .Include(j => j.Ganador)
                .ToListAsync();

            await db.DisposeAsync();

            var sb = new StringBuilder();

            // CSV de Jugadores
            sb.AppendLine("=== JUGADORES ===");
            sb.AppendLine("ID,Alias,Pais,RazaPrincipal");
            foreach (var j in jugadores)
                sb.AppendLine($"{j.IdJugador},\"{j.Alias}\",\"{j.Pais}\",\"{j.RazaPrincipal}\"");

            sb.AppendLine();

            // CSV de Mapas
            sb.AppendLine("=== MAPAS ===");
            sb.AppendLine("ID,Nombre");
            foreach (var m in mapas)
                sb.AppendLine($"{m.IdMapa},\"{m.Nombre}\"");

            sb.AppendLine();

            // CSV de Series
            sb.AppendLine("=== SERIES ===");
            sb.AppendLine("ID,Modalidad,Fecha,Jugador1,Jugador2");
            foreach (var s in series)
                sb.AppendLine($"{s.IdSerie},\"{s.Modalidad}\",{s.Fecha:yyyy-MM-dd},\"{s.Jugador1?.Alias}\",\"{s.Jugador2?.Alias}\"");

            sb.AppendLine();

            // CSV de Juegos
            sb.AppendLine("=== JUEGOS ===");
            sb.AppendLine("ID,Serie,Mapa,RazaJ1,RazaJ2,Ganador,Fecha");
            foreach (var g in juegos)
                sb.AppendLine($"{g.IdJuego},{g.IdSerie},\"{g.Mapa?.Nombre}\",\"{g.RazaJugador1}\",\"{g.RazaJugador2}\",\"{g.Ganador?.Alias}\",{g.FechaCreacion:yyyy-MM-dd HH:mm}");

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var nombreArchivo = $"starcraft_backup_{timestamp}.csv";
            var rutaArchivo = Path.Combine(FileSystem.AppDataDirectory, nombreArchivo);

            await File.WriteAllTextAsync(rutaArchivo, sb.ToString(), Encoding.UTF8);

            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Exportar a CSV",
                File = new ShareFile(rutaArchivo)
            });

            GuardarFechaBackup();
            await DisplayAlert("✅ Éxito",
                $"Datos exportados a CSV.\n\nArchivo: {nombreArchivo}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("❌ Error", $"No se pudo exportar:\n{ex.Message}", "OK");
        }
        finally
        {
            var button = sender as Button;
            button.IsEnabled = true;
            button.Text = "📊 CSV";
        }
    }

    private async void OnExportarXMLClicked(object sender, EventArgs e)
    {
        try
        {
            var button = sender as Button;
            button.IsEnabled = false;
            button.Text = "📄 Exportando...";

            var db = new AppDbContext();

            var jugadores = await db.Jugadores.ToListAsync();
            var mapas = await db.Mapas.ToListAsync();
            var series = await db.Series.ToListAsync();
            var juegos = await db.Juegos.ToListAsync();

            await db.DisposeAsync();

            var xml = new XDocument(
                new XElement("StarcraftBackup",
                    new XAttribute("FechaExportacion", DateTime.Now.ToString("o")),
                    new XAttribute("Version", "1.0"),

                    new XElement("Jugadores",
                        jugadores.Select(j => new XElement("Jugador",
                            new XAttribute("Id", j.IdJugador),
                            new XElement("Alias", j.Alias),
                            new XElement("Pais", j.Pais ?? ""),
                            new XElement("RazaPrincipal", j.RazaPrincipal ?? "")
                        ))
                    ),

                    new XElement("Mapas",
                        mapas.Select(m => new XElement("Mapa",
                            new XAttribute("Id", m.IdMapa),
                            new XElement("Nombre", m.Nombre)
                        ))
                    ),

                    new XElement("Series",
                        series.Select(s => new XElement("Serie",
                            new XAttribute("Id", s.IdSerie),
                            new XElement("Modalidad", s.Modalidad),
                            new XElement("Fecha", s.Fecha.ToString("o")),
                            new XElement("IdJugador1", s.IdJugador1),
                            new XElement("IdJugador2", s.IdJugador2)
                        ))
                    ),

                    new XElement("Juegos",
                        juegos.Select(g => new XElement("Juego",
                            new XAttribute("Id", g.IdJuego),
                            new XElement("IdSerie", g.IdSerie),
                            new XElement("IdMapa", g.IdMapa),
                            new XElement("RazaJugador1", g.RazaJugador1 ?? ""),
                            new XElement("RazaJugador2", g.RazaJugador2 ?? ""),
                            new XElement("IdGanador", g.IdGanador),
                            new XElement("FechaCreacion", g.FechaCreacion.ToString("o"))
                        ))
                    )
                )
            );

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var nombreArchivo = $"starcraft_backup_{timestamp}.xml";
            var rutaArchivo = Path.Combine(FileSystem.AppDataDirectory, nombreArchivo);

            xml.Save(rutaArchivo);

            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Exportar a XML",
                File = new ShareFile(rutaArchivo)
            });

            GuardarFechaBackup();
            await DisplayAlert("✅ Éxito",
                $"Datos exportados a XML.\n\nArchivo: {nombreArchivo}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("❌ Error", $"No se pudo exportar:\n{ex.Message}", "OK");
        }
        finally
        {
            var button = sender as Button;
            button.IsEnabled = true;
            button.Text = "📄 XML";
        }
    }

    // ==================== IMPORTAR ====================

    private async void OnImportarSQLiteClicked(object sender, EventArgs e)
    {
        try
        {
            bool confirmar = await DisplayAlert(
                "⚠️ CONFIRMACIÓN REQUERIDA",
                "Esta acción eliminará TODOS los datos actuales y los reemplazará con los del archivo importado.\n\n" +
                "¿Estás COMPLETAMENTE seguro?",
                "Sí, importar",
                "Cancelar"
            );

            if (!confirmar) return;

            var resultado = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Selecciona archivo de base de datos",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".db", ".sqlite", ".sqlite3" } },
                    { DevicePlatform.Android, new[] { "application/x-sqlite3", "application/vnd.sqlite3" } },
                    { DevicePlatform.iOS, new[] { "public.database" } }
                })
            });

            if (resultado == null) return;

            var button = sender as Button;
            button.IsEnabled = false;
            button.Text = "📁 Importando...";

            var dbInfo = AppDbContext.GetDatabaseInfo();
            var lines = dbInfo.Split('\n');
            var rutaDestino = lines.FirstOrDefault(l => l.StartsWith("Ruta:"))?.Replace("Ruta:", "").Trim();

            if (string.IsNullOrEmpty(rutaDestino))
            {
                await DisplayAlert("❌ Error", "No se pudo determinar la ruta de la base de datos.", "OK");
                return;
            }

            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            await Task.Delay(500);

            int intentos = 0;
            int maxIntentos = 5;
            bool copiado = false;

            while (intentos < maxIntentos && !copiado)
            {
                try
                {
                    using (var stream = await resultado.OpenReadAsync())
                    using (var fileStream = new FileStream(rutaDestino, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await stream.CopyToAsync(fileStream);
                        await fileStream.FlushAsync();
                    }
                    copiado = true;
                }
                catch (IOException) when (intentos < maxIntentos - 1)
                {
                    intentos++;
                    await Task.Delay(1000); 
                }
            }

            if (!copiado)
            {
                await DisplayAlert("❌ Error",
                    "No se pudo reemplazar la base de datos después de varios intentos.\n\n" +
                    "Intenta cerrar y volver a abrir la aplicación.", "OK");
                return;
            }

            await DisplayAlert("✅ Éxito",
                "Base de datos importada correctamente.\n\nLa aplicación se reiniciará.", "OK");

            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            Application.Current.MainPage = new AppShell();
        }
        catch (Exception ex)
        {
            await DisplayAlert("❌ Error", $"No se pudo importar:\n{ex.Message}", "OK");
        }
        finally
        {
            var button = sender as Button;
            button.IsEnabled = true;
            button.Text = "📁 Desde SQLite";
        }
    }

    private async void OnImportarJSONClicked(object sender, EventArgs e)
    {
        try
        {
            bool confirmar = await DisplayAlert(
                "⚠️ CONFIRMACIÓN REQUERIDA",
                "Esta acción eliminará TODOS los datos actuales y los reemplazará con los del archivo JSON.\n\n" +
                "¿Estás COMPLETAMENTE seguro?",
                "Sí, importar",
                "Cancelar"
            );

            if (!confirmar) return;

            var resultado = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Selecciona archivo JSON",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".json" } },
                    { DevicePlatform.Android, new[] { "application/json" } },
                    { DevicePlatform.iOS, new[] { "public.json" } }
                })
            });

            if (resultado == null) return;

            var button = sender as Button;
            button.IsEnabled = false;
            button.Text = "📋 Importando...";

            string json;
            using (var stream = await resultado.OpenReadAsync())
            using (var reader = new StreamReader(stream))
            {
                json = await reader.ReadToEndAsync();
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var data = JsonSerializer.Deserialize<BackupData>(json, options);

            if (data == null)
            {
                await DisplayAlert("❌ Error", "El archivo JSON no tiene el formato correcto.", "OK");
                return;
            }

            var db = new AppDbContext();

            try
            {
                db.Juegos.RemoveRange(db.Juegos);
                await db.SaveChangesAsync();

                db.Series.RemoveRange(db.Series);
                await db.SaveChangesAsync();

                db.Mapas.RemoveRange(db.Mapas);
                await db.SaveChangesAsync();

                db.Jugadores.RemoveRange(db.Jugadores);
                await db.SaveChangesAsync();

                if (data.Jugadores != null && data.Jugadores.Count > 0)
                {
                    db.Jugadores.AddRange(data.Jugadores);
                    await db.SaveChangesAsync();
                }

                if (data.Mapas != null && data.Mapas.Count > 0)
                {
                    db.Mapas.AddRange(data.Mapas);
                    await db.SaveChangesAsync();
                }

                if (data.Series != null && data.Series.Count > 0)
                {
                    db.Series.AddRange(data.Series);
                    await db.SaveChangesAsync();
                }

                if (data.Juegos != null && data.Juegos.Count > 0)
                {
                    db.Juegos.AddRange(data.Juegos);
                    await db.SaveChangesAsync();
                }
            }
            finally
            {
                await db.DisposeAsync();
            }

            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            await DisplayAlert("✅ Éxito",
                $"Datos importados correctamente:\n\n" +
                $"• Jugadores: {data.Jugadores?.Count ?? 0}\n" +
                $"• Mapas: {data.Mapas?.Count ?? 0}\n" +
                $"• Series: {data.Series?.Count ?? 0}\n" +
                $"• Juegos: {data.Juegos?.Count ?? 0}\n\n" +
                $"La aplicación se reiniciará.", "OK");

            Application.Current.MainPage = new AppShell();
        }
        catch (Exception ex)
        {
            await DisplayAlert("❌ Error", $"No se pudo importar:\n{ex.Message}", "OK");
        }
        finally
        {
            var button = sender as Button;
            button.IsEnabled = true;
            button.Text = "📋 Desde JSON";
        }
    }

    private class BackupData
    {
        public DateTime FechaExportacion { get; set; }
        public string Version { get; set; }
        public List<Jugador> Jugadores { get; set; }
        public List<Mapa> Mapas { get; set; }
        public List<Serie> Series { get; set; }
        public List<Juego> Juegos { get; set; }
    }
}