using StarCraft.Data;
using Microsoft.EntityFrameworkCore;

namespace StarCraft.Views;

public partial class DiagnosticsPage : ContentPage
{
    public DiagnosticsPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        CargarDiagnostico();
    }

    private async void CargarDiagnostico()
    {
        try
        {
            var info = AppDbContext.GetDatabaseInfo();

            using var db = new AppDbContext();

            var jugadores = await db.Jugadores.CountAsync();
            var mapas = await db.Mapas.CountAsync();
            var series = await db.Series.CountAsync();
            var juegos = await db.Juegos.CountAsync();

            var diagnostico = $@"
🗄️ INFORMACIÓN DE BASE DE DATOS
═══════════════════════════════════

{info}

📊 REGISTROS:
• Jugadores: {jugadores}
• Mapas: {mapas}
• Series: {series}
• Juegos: {juegos}

📱 INFORMACIÓN DEL SISTEMA:
• Plataforma: {DeviceInfo.Platform}
• Versión: {DeviceInfo.VersionString}
• Modelo: {DeviceInfo.Model}
• Fabricante: {DeviceInfo.Manufacturer}

📁 RUTAS:
• AppData: {FileSystem.AppDataDirectory}
• Cache: {FileSystem.CacheDirectory}
";

            LblDiagnostico.Text = diagnostico;
        }
        catch (Exception ex)
        {
            LblDiagnostico.Text = $"❌ ERROR:\n\n{ex.Message}\n\n{ex.StackTrace}";
        }
    }

    private async void OnRecrearDBClicked(object sender, EventArgs e)
    {
        bool confirmar = await DisplayAlert(
            "⚠️ ADVERTENCIA",
            "Esto eliminará TODOS los datos y creará una nueva base de datos.\n\n¿Estás seguro?",
            "Sí, recrear",
            "Cancelar"
        );

        if (!confirmar) return;

        try
        {
            using var db = new AppDbContext();
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();

            await DisplayAlert("✅ Éxito", "Base de datos recreada correctamente.", "OK");
            CargarDiagnostico();
        }
        catch (Exception ex)
        {
            await DisplayAlert("❌ Error", $"No se pudo recrear la base de datos:\n\n{ex.Message}", "OK");
        }
    }

    private void OnCopiarInfoClicked(object sender, EventArgs e)
    {
        Clipboard.SetTextAsync(LblDiagnostico.Text);
        DisplayAlert("📋 Copiado", "Información copiada al portapapeles.", "OK");
    }
}