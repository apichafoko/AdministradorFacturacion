using FacturacionSimple.Helpers;
using FacturacionSimple.Models;
using System.Text;
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);


// Configurar el tamaño máximo de la solicitud
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 104857600; // 100 MB
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
});


// Add services to the container.
builder.Services.AddControllersWithViews()
        .AddRazorRuntimeCompilation();


builder.Services.AddTransient<BoletaProcessor>();
builder.Services.AddTransient<Entidades>();

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
builder.Services.AddSignalR(); // Añadir SignalR


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
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

app.Run();

