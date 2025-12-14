using StarCraft.Data;
using Microsoft.Data.Sqlite;
using System.Diagnostics;

namespace StarCraft
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // Verificar si hay una restauración pendiente
            VerificarRestauracionPendiente();

            // Inicializar base de datos
            InicializarBaseDeDatos();

            MainPage = new AppShell();
        }

        private void VerificarRestauracionPendiente()
        {
            try
            {
                var flagPath = Path.Combine(FileSystem.AppDataDirectory, "restore_pending.flag");

                if (File.Exists(flagPath))
                {
                    Debug.WriteLine("[APP] Restauración pendiente detectada...");

                    var tempDbPath = File.ReadAllText(flagPath);

                    if (File.Exists(tempDbPath))
                    {
                        // Obtener ruta de BD actual
                        var dbInfo = AppDbContext.GetDatabaseInfo();
                        var lines = dbInfo.Split('\n');
                        var rutaDestino = lines.FirstOrDefault(l => l.StartsWith("Ruta:"))?.Replace("Ruta:", "").Trim();

                        if (!string.IsNullOrEmpty(rutaDestino))
                        {
                            // Cerrar todas las conexiones
                            SqliteConnection.ClearAllPools();
                            GC.Collect();
                            GC.WaitForPendingFinalizers();

                            Thread.Sleep(500);

                            // Reemplazar base de datos
                            File.Copy(tempDbPath, rutaDestino, true);

                            Debug.WriteLine("[APP] Base de datos restaurada ✓");

                            // Limpiar archivos temporales
                            File.Delete(tempDbPath);
                            File.Delete(flagPath);

                            MainThread.BeginInvokeOnMainThread(async () =>
                            {
                                await Task.Delay(1000);
                                if (MainPage != null)
                                {
                                    await MainPage.DisplayAlert(
                                        "✅ Restauración Completa",
                                        "La base de datos ha sido restaurada exitosamente.",
                                        "OK"
                                    );
                                }
                            });
                        }
                    }
                    else
                    {
                        // Limpiar flag si no hay archivo temporal
                        File.Delete(flagPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP ERROR] Error en restauración: {ex.Message}");
            }
        }

        private void InicializarBaseDeDatos()
        {
            try
            {
                Debug.WriteLine("[APP] Inicializando base de datos...");

                // Crear una instancia del contexto para forzar la creación de la DB
                using (var db = new AppDbContext())
                {
                    // Verificar que la base de datos existe
                    var canConnect = db.Database.CanConnect();

                    Debug.WriteLine($"[APP] Base de datos conectada: {canConnect}");
                    Debug.WriteLine($"[APP] {AppDbContext.GetDatabaseInfo()}");

                    if (!canConnect)
                    {
                        Debug.WriteLine("[APP] Creando base de datos...");
                        db.Database.EnsureCreated();
                    }

                    // Verificar tablas
                    var jugadoresCount = db.Jugadores.Count();
                    var mapasCount = db.Mapas.Count();
                    var seriesCount = db.Series.Count();
                    var juegosCount = db.Juegos.Count();

                    Debug.WriteLine($"[APP] Registros actuales:");
                    Debug.WriteLine($"  - Jugadores: {jugadoresCount}");
                    Debug.WriteLine($"  - Mapas: {mapasCount}");
                    Debug.WriteLine($"  - Series: {seriesCount}");
                    Debug.WriteLine($"  - Juegos: {juegosCount}");
                }

                Debug.WriteLine("[APP] Base de datos inicializada correctamente ✓");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP ERROR] Error al inicializar base de datos: {ex.Message}");
                Debug.WriteLine($"[APP ERROR] StackTrace: {ex.StackTrace}");

                // Mostrar alerta al usuario
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    if (MainPage != null)
                    {
                        await MainPage.DisplayAlert(
                            "❌ Error de Base de Datos",
                            $"No se pudo inicializar la base de datos:\n\n{ex.Message}\n\n" +
                            $"La aplicación puede no funcionar correctamente.",
                            "OK"
                        );
                    }
                });
            }
        }
    }
}