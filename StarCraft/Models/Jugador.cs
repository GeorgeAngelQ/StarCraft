using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace StarCraft.Models
{
    [Index(nameof(Alias), IsUnique = true)]
    public partial class Jugador
    {
        [Key]
        public int IdJugador { get; set; }
        public string Alias { get; set; } = string.Empty;
        public string? Pais { get; set; }
        public string? RazaPrincipal { get; set; } 
        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        public override string? ToString() => Alias ?? base.ToString();
    }
}
