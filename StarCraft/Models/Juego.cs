using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StarCraft.Models
{
    public class Juego
    {
        [Key]
        public int IdJuego { get; set; }

        [ForeignKey(nameof(Serie))]
        public int IdSerie { get; set; }
        public Serie? Serie { get; set; }

        [ForeignKey(nameof(Mapa))]
        public int IdMapa { get; set; }
        public Mapa? Mapa { get; set; }

        public string? RazaJugador1 { get; set; } = string.Empty;
        public string? RazaJugador2 { get; set; } = string.Empty; 

        [ForeignKey(nameof(Ganador))]
        public int IdGanador { get; set; }
        public Jugador? Ganador { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.Now;

    }
}
