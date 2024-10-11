using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using FacturacionSimple.Models;
using FacturacionSimple.Helpers;
using NPOI.SS.Formula.Functions;

namespace FacturacionSimple.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IWebHostEnvironment _hostingEnvironment;
    private readonly BoletaProcessor _boletaProcessor;

    private readonly Entidades _Entidades;

    
    public HomeController(ILogger<HomeController> logger, IWebHostEnvironment hostingEnvironment, BoletaProcessor boletaProcessor, Entidades entidades)
    {
        _logger = logger;
        _hostingEnvironment = hostingEnvironment ?? throw new ArgumentNullException(nameof(hostingEnvironment));
        _boletaProcessor = boletaProcessor;
        _Entidades = entidades;

    }

    public async Task<IActionResult> Index()
    {
        var lViewModel = new IndexViewModel
        {
            ListadoBoletas = [],
            SaldosMensualesPublico = []
        };

            
        var uploadsFolder = Path.Combine(_hostingEnvironment.ContentRootPath, "Files");

        var filePath = Path.Combine(uploadsFolder, "_BoletasEstados-11.xlsx");
    

        var boletas = await _boletaProcessor.ProcesarArchivo(filePath);
        var lEntidades = _boletaProcessor.GetEntidades(boletas);

        //Obtengo las Entidades Privadas
        var lEntidadesPrivadas = _Entidades.GetEntidadesPrivadas(lEntidades);
        //Obtengo las Entidades Publicas
        var lEntidadesPublicas = _Entidades.GetEntidadesPublicas(lEntidades);

        //Obtengo el ultimo periodo
        var lastPeriod = boletas.OrderByDescending(s => DateTime.Parse(s.Periodo)).First();

        var lastMonth = lastPeriod.PeriodoMes;
        var lastYear = lastPeriod.PeriodoAnio;  

        lViewModel.LastPeriod = lastMonth;
        lViewModel.LastYear = lastYear;
        
        
        #region Datos Generales
        
        lViewModel.ListadoBoletas = boletas;
        lViewModel.CantidadBoletasConPagos = boletas.Where(x => x.Cobrado > 0).Count();
        lViewModel.CantidadBoletasProceadas = boletas.Count();
        lViewModel.CantidadBoletasDia = _boletaProcessor.CantidadBoletasDiaSemana(boletas);
        lViewModel.CantidadBoletasDiaLastPeriod = _boletaProcessor.CantidadBoletasDiaSemana(boletas.Where(b => b.PeriodoAnio == lastYear && b.PeriodoMes == lastMonth).ToList());
        #endregion

        #region Datos Públicos
        lViewModel.CantidadBoletasPublicas = boletas
            .Where(b => lEntidadesPublicas.Any(e => e.Codigo == b.Entidad.Codigo) &&
                b.PeriodoAnio == lastYear &&
                b.PeriodoMes == lastMonth)
            .Count();
        
        lViewModel.PromedioBoletaUltimoMesPublico = boletas
            .Where(b => lEntidadesPublicas.Any(e => e.Codigo == b.Entidad.Codigo) &&
                b.PeriodoAnio == lastYear &&
                b.PeriodoMes == lastMonth)
            .Select(b => b.Facturado)
            .Average();

        lViewModel.BoletaMenorValorU3MPublico = _boletaProcessor.GetBoletaMenorValorUltimos3Meses(boletas.Where(b => lEntidadesPublicas.Any(e => e.Codigo == b.Entidad.Codigo)).ToList());
        lViewModel.BoletaMayorValorU3MPublico = _boletaProcessor.GetBoletaMayorValorUltimos3Meses(boletas.Where(b => lEntidadesPublicas.Any(e => e.Codigo == b.Entidad.Codigo)).ToList());
        var ultimoPeriodoPublico = boletas
            .Where(b => lEntidadesPublicas.Any(e => e.Codigo == b.Entidad.Codigo) &&
                b.PeriodoAnio == lastYear &&
                b.PeriodoMes == lastMonth)
            .ToList();

        lViewModel.IngresoPromedioUltimoBrutoPublico = ultimoPeriodoPublico
            .Select(b => b.Facturado)
            .Sum();

        lViewModel.SaldosMensualesPublico = _boletaProcessor.GetSaldosMensuales(boletas.Where(b => lEntidadesPublicas.Any(e => e.Codigo == b.Entidad.Codigo)).ToList())
            .OrderByDescending(s => DateTime.Parse(s.Periodo))
            .ToList();



        lViewModel.IngresosMensualesPublico = lViewModel.SaldosMensualesPublico
            .OrderBy(s => DateTime.Parse(s.Periodo))
            .TakeLast(12)
            .ToDictionary(s => DateTime.Parse(s.Periodo), s => s.ImporteFacturadoBruto);

        lViewModel.CantidadBoletasMensualesPublico = _boletaProcessor.GetBoletasMensualesPublico(boletas.Where(b => lEntidadesPublicas.Any(e => e.Codigo == b.Entidad.Codigo)).ToList()).OrderBy(x => x.Key).TakeLast(12).ToDictionary(x => x.Key, x => x.Value);


        lViewModel.CantidadBoletasPorMutualPublico = boletas
            .Where(b => lEntidadesPublicas.Any(e => e.Codigo == b.Entidad.Codigo) &&
                b.PeriodoAnio == lastYear &&
                b.PeriodoMes == lastMonth)
            .GroupBy(b => b.Entidad.Nombre)
            .ToDictionary(g => g.Key, g => g.Count());

        lViewModel.MontosProximosACobrarPublico = _boletaProcessor.GetMontosPorPeriodo(boletas.Where(b => lEntidadesPrivadas.Any(e => e.Codigo == b.Entidad.Codigo)).ToList(), lastYear, lastMonth);


        #endregion

        #region Privados

        #endregion

        return View(lViewModel);
        }

        public IActionResult Privacy()
        {
        return View();
        }
        
        [HttpPost]
        public async Task<IActionResult> Index(IFormFile file)
        {
        var lViewModel = new IndexViewModel();
        lViewModel.ListadoBoletas = new List<Boleta>();

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

            var filePath = Path.Combine(uploadsFolder, file.FileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }


            // Llamar al procesador de boletas para leer y procesar el archivo
            var boletas = await _boletaProcessor.ProcesarArchivo(filePath);

            lViewModel.ListadoBoletas = boletas;
            lViewModel.CantidadBoletasConPagos = boletas.Where(x => x.Cobrado > 0).Count();
            lViewModel.CantidadBoletasProceadas = boletas.Count();
            lViewModel.CantidadBoletasDia = _boletaProcessor.CantidadBoletasDiaSemana(boletas);
            

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



        return View(lViewModel);

    }


    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

}

