using FacturacionAdmin.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FacturacionAdmin.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Agregá un DbSet para la nueva clase
    public DbSet<Entidad> Entidades { get; set; }
    public DbSet<Boleta> Boletas { get; set; }
    public DbSet<Profesional> Profesionales { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuración adicional si es necesario
    }
}

