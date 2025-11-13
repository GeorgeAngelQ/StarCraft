using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StarCraft.Models
{
    public class Serie
    {
        [Key]
        public int IdSerie { get; set; }

        [ForeignKey(nameof(Jugador1))]
        public int IdJugador1 { get; set; }
        public Jugador? Jugador1 { get; set; }

        [ForeignKey(nameof(Jugador2))]
        public int IdJugador2 { get; set; }
        public Jugador? Jugador2 { get; set; }

        public DateTime Fecha { get; set; }
        public string Modalidad { get; set; } = string.Empty;

        public ICollection<Juego>? Juegos { get; set; }

    }
}
