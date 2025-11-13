using Microsoft.Extensions.Logging;
using StarCraft.Data;

namespace StarCraft
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");

                });

            using (var db = new AppDbContext())
            {
                db.Database.EnsureCreated();
            }
            return builder.Build();
        }
    }
}
