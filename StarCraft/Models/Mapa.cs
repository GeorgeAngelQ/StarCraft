using System.ComponentModel.DataAnnotations;

namespace StarCraft.Models
{
    public class Mapa
    {
        [Key]
        public int IdMapa { get; set; }
        public string Nombre { get; set; } = string.Empty;

    }
}
