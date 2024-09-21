using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using FacturacionAdmin.Models;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using FacturacionAdmin.Data;
using FacturacionAdmin.Helpers;

namespace FacturacionAdmin.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IWebHostEnvironment _hostingEnvironment;
    private readonly BoletaProcessor _boletaProcessor;

    public HomeController(ILogger<HomeController> logger, IWebHostEnvironment hostingEnvironment, BoletaProcessor boletaProcessor)
    {
        _logger = logger;
        _hostingEnvironment = hostingEnvironment ?? throw new ArgumentNullException(nameof(hostingEnvironment));
        _boletaProcessor = boletaProcessor;


    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult Upload()
    {
        return View();
    }


    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                ViewBag.Message = "Por favor seleccioná un archivo.";
                return View();
            }

            var uploadsFolder = Path.Combine(_hostingEnvironment.ContentRootPath, "Files");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var filePath = Path.Combine(uploadsFolder, User.Identity.Name + "_" + file.FileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }


            // Llamar al procesador de boletas para leer y procesar el archivo
            await _boletaProcessor.ProcesarArchivo(filePath, Convert.ToInt32(User.Identity.Name));

            ViewBag.Message = "Archivo subido exitosamente.";
        }
        catch (Exception ex)
        {
            // Capturar cualquier excepción y registrar el error internamente si es necesario
            // Aquí puedes usar un logger para registrar el error
            // _logger.LogError(ex, "Error al procesar el archivo");

            // Mostrar un mensaje genérico al usuario sin exponer el detalle del error
            ViewBag.Message = "Ocurrió un error al procesar el archivo. Por favor, intente nuevamente.";
        }
       


        return View();

    }


    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }



   
}

