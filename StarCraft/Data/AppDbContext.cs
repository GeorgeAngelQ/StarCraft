using Microsoft.EntityFrameworkCore;
using StarCraft.Models;

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
                    Directory.CreateDirectory(folder);

                return Path.Combine(folder, "starcraft.db");
            }
        }

        public AppDbContext() { }

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlite($"Filename={DbPath}");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Juego>()
                .HasOne(j => j.Serie)
                .WithMany(s => s.Juegos)
                .HasForeignKey(j => j.IdSerie);

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
        }
    }
}