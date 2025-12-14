using Microsoft.EntityFrameworkCore;
using StarCraft.Models;
using System.Diagnostics;

namespace StarCraft.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Jugador> Jugadores { get; set; }
        public DbSet<Mapa> Mapas { get; set; }
        public DbSet<Juego> Juegos { get; set; }
        public DbSet<Serie> Series { get; set; }

        private static string DbPath
        {
            get
            {
                var folder = FileSystem.AppDataDirectory;

                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                var dbPath = Path.Combine(folder, "starcraft.db");

                Debug.WriteLine($"[DB] Ruta de base de datos: {dbPath}");

                return dbPath;
            }
        }

        public AppDbContext()
        {
            try
            {
                Database.EnsureCreated();
                Debug.WriteLine("[DB] Base de datos inicializada correctamente");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DB ERROR] Error al crear base de datos: {ex.Message}");
                throw;
            }
        }

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
            try
            {
                Database.EnsureCreated();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DB ERROR] Error al crear base de datos: {ex.Message}");
                throw;
            }
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var dbPath = DbPath;
                optionsBuilder.UseSqlite($"Data Source={dbPath}");

#if DEBUG
                optionsBuilder.LogTo(message => Debug.WriteLine(message));
#endif
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Juego>()
                .HasOne(j => j.Serie)
                .WithMany(s => s.Juegos)
                .HasForeignKey(j => j.IdSerie)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Serie>()
                .HasOne(s => s.Jugador1)
                .WithMany()
                .HasForeignKey(s => s.IdJugador1)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Serie>()
                .HasOne(s => s.Jugador2)
                .WithMany()
                .HasForeignKey(s => s.IdJugador2)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Juego>()
                .HasOne(j => j.Mapa)
                .WithMany()
                .HasForeignKey(j => j.IdMapa)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Juego>()
                .HasOne(j => j.Ganador)
                .WithMany()
                .HasForeignKey(j => j.IdGanador)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Jugador>()
                .HasIndex(j => j.Alias)
                .IsUnique();

            modelBuilder.Entity<Mapa>()
                .HasIndex(m => m.Nombre)
                .IsUnique();

            modelBuilder.Entity<Serie>()
                .HasIndex(s => s.Fecha);

            modelBuilder.Entity<Juego>()
                .HasIndex(j => j.FechaCreacion);
        }

        public static string GetDatabaseInfo()
        {
            var dbPath = DbPath;
            var exists = File.Exists(dbPath);
            var size = exists ? new FileInfo(dbPath).Length : 0;

            return $"Ruta: {dbPath}\nExiste: {exists}\nTamaño: {size / 1024.0:F2} KB";
        }
    }
}