using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using FacturacionAdmin.Data;
using System.Configuration;
using FacturacionAdmin.Models;
using FacturacionAdmin.Helpers;
using System.Text;

var builder = WebApplication.CreateBuilder(args);


/*
// Configura el contexto de la base de datos para usar PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Configura Identity
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>();
*/
// Otros servicios



// Configura el contexto de la base de datos para usar PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Configura Identity
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>();

// Otros servicios
builder.Services.AddControllersWithViews();

builder.Services.AddTransient<BoletaProcessor>();
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
builder.Services.AddSignalR(); // Añadir SignalR


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

// Mapea el Hub de SignalR
app.MapHub<ProgressHub>("/progressHub");

app.Run();



