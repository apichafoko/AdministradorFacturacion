using FacturacionSimple.Helpers;
using FacturacionSimple.Models;
using System.Text;
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);


// Configurar el tamaño máximo de la solicitud y timeouts para archivos grandes
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 104857600; // 100 MB
    serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5); // Timeout de encabezados
    serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(5); // Timeout de keep-alive
});

// O si estás usando IIS
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 104857600; // 100 MB
});

// Si usas formularios multiparte (uploads de archivos por ejemplo)
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104857600; // 100 MB para multipart/form-data
    options.ValueLengthLimit = int.MaxValue; // Longitud máxima de valores
    options.MultipartBodyLengthLimit = 104857600; // 100 MB
    options.MultipartHeadersLengthLimit = int.MaxValue; // Longitud máxima de encabezados
});


// Add services to the container.
builder.Services.AddControllersWithViews()
        .AddRazorRuntimeCompilation();

// Configurar sesión para TempData
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddTransient<BoletaProcessor>();
builder.Services.AddTransient<Entidades>();

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
builder.Services.AddSignalR(); // Añadir SignalR


var app = builder.Build();

// Manejador global de excepciones para evitar crashes
app.UseExceptionHandler("/Home/Error");

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession(); // Habilitar sesión para TempData

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

