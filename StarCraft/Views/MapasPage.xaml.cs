using Microsoft.EntityFrameworkCore;
using StarCraft.Data;
using StarCraft.Models;

namespace StarCraft.Views
{
    public partial class MapasPage : ContentPage
    {
        private readonly AppDbContext _context;
        private List<Mapa> _mapasOriginales = new();

        public MapasPage()
        {
            InitializeComponent();
            _context = new AppDbContext();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await CargarMapas();
        }

        private async Task CargarMapas()
        {
            _mapasOriginales = await _context.Mapas
                .OrderBy(m => m.Nombre)
                .ToListAsync();

            MapasCollection.ItemsSource = _mapasOriginales;
        }
        private void Campo_Completed(object sender, EventArgs e)
        {
            OnRegistrarClicked(sender, e);
        }
        private async void OnRegistrarClicked(object sender, EventArgs e)
        {
            var nombre = NombreEntry.Text?.Trim();

            if (string.IsNullOrWhiteSpace(nombre))
            {
                await DisplayAlert("Aviso", "Por favor, ingresa un nombre de mapa.", "OK");
                return;
            }

            bool existe = await _context.Mapas.AnyAsync(m => m.Nombre.ToLower() == nombre.ToLower());
            if (existe)
            {
                await DisplayAlert("Aviso", "Ese mapa ya existe.", "OK");
                return;
            }

            var mapa = new Mapa { Nombre = nombre };
            _context.Mapas.Add(mapa);
            await _context.SaveChangesAsync();

            NombreEntry.Text = string.Empty;
            await CargarMapas();
        }

        private void OnBuscarMapa(object sender, TextChangedEventArgs e)
        {
            var texto = e.NewTextValue?.ToLower() ?? "";
            MapasCollection.ItemsSource = _mapasOriginales
                .Where(m => m.Nombre.ToLower().Contains(texto))
                .ToList();
        }

        private async void OnEliminarClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is int id)
            {
                var mapa = await _context.Mapas.FindAsync(id);
                if (mapa != null)
                {
                    bool confirm = await DisplayAlert("Confirmar", $"¿Eliminar el mapa '{mapa.Nombre}'?", "Sí", "No");
                    if (confirm)
                    {
                        _context.Mapas.Remove(mapa);
                        await _context.SaveChangesAsync();
                        await CargarMapas();
                    }
                }
            }
        }
    }
}
