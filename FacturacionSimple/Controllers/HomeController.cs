using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using FacturacionSimple.Models;
using FacturacionSimple.Helpers;
using NPOI.SS.Formula.Functions;
using Newtonsoft.Json;

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
            SaldosHistoricosPublicos = [],
            SaldosHistoricosPrivados = []
        };

            
        var uploadsFolder = Path.Combine(_hostingEnvironment.ContentRootPath, "Files");

        var filePath = Path.Combine(uploadsFolder, "_BoletasEstados-11.xlsx");
    
        // Procesa el archivo y obtiene la lista de boletas
        var boletas = await _boletaProcessor.ProcesarArchivo(filePath);

        // Obtiene las entidades a partir de las boletas procesadas
        var lEntidades = _boletaProcessor.GetEntidades(boletas);

        // Obtiene las entidades privadas
        var lEntidadesPrivadas = _Entidades.GetEntidadesPrivadas(lEntidades);

        // Obtiene las entidades públicas
        var lEntidadesPublicas = _Entidades.GetEntidadesPublicas(lEntidades);

        // Obtiene el último periodo de las boletas
        var lastPeriod = boletas.OrderByDescending(s => DateTime.Parse(s.Periodo)).First();

        // Obtiene el último mes y año del periodo
        var lastMonth = lastPeriod.PeriodoMes;
        var lastYear = lastPeriod.PeriodoAnio;

        // Asigna el último periodo y año al ViewModel
        lViewModel.LastPeriod = lastMonth;
        lViewModel.LastYear = lastYear;

        // Genera un listado de fechas correspondiente al primer día de cada mes de todos los periodos que existan en el listado de boletas
        var fechasPrimerDiaMes = boletas
            .Select(b => new DateTime(b.PeriodoAnio, b.PeriodoMes, 1))
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        
        var lCotizaciones = await GetCotizacionDolar(fechasPrimerDiaMes, _hostingEnvironment).ConfigureAwait(false);


        var lSaldosTotales = _boletaProcessor.GetSaldosMensuales(boletas);

        var lIngresosTotalesUSD = _boletaProcessor.GetFacturacionenUSD(lSaldosTotales,lCotizaciones);

        // Ordena y asigna los ingresos mensuales públicos al ViewModel 
        lViewModel.IngresosTotalesUSD = lIngresosTotalesUSD;
        lViewModel.CotizacionesUSD = lCotizaciones;
        lViewModel.CotizacionUSDLast = lCotizaciones.OrderByDescending(x => x.Key).First().Value;


        #region Datos Generales

        // Asigna la lista de boletas al ViewModel
        lViewModel.ListadoBoletas = boletas;

        // Cuenta la cantidad de boletas con pagos y las asigna al ViewModel
        lViewModel.CantidadBoletasConPagos = boletas.Where(x => x.Cobrado > 0).Count();

        // Cuenta la cantidad total de boletas procesadas y las asigna al ViewModel
        lViewModel.CantidadBoletasProceadas = boletas.Count();

        // Cuenta la cantidad de boletas por día de la semana y las asigna al ViewModel
        lViewModel.CantidadBoletasDia = _boletaProcessor.CantidadBoletasDiaSemana(boletas);

        // Cuenta la cantidad de boletas por día de la semana del último periodo y las asigna al ViewModel
        lViewModel.CantidadBoletasDiaLastPeriod = _boletaProcessor.CantidadBoletasDiaSemana(boletas.Where(b => b.PeriodoAnio == lastYear && b.PeriodoMes == lastMonth).ToList());

        lViewModel.CantidadBoletasDiaPublico = _boletaProcessor.CantidadBoletasDiaSemana(
            boletas.Where(b => lEntidadesPublicas.Any(e => e.Codigo == b.Entidad.Codigo) &&
                   b.PeriodoAnio == lastYear &&
                   b.PeriodoMes == lastMonth).ToList());

        lViewModel.CantidadBoletasDiaPrivado = _boletaProcessor.CantidadBoletasDiaSemana(
            boletas.Where(b => lEntidadesPrivadas.Any(e => e.Codigo == b.Entidad.Codigo) &&
                       b.PeriodoAnio == lastYear &&
                       b.PeriodoMes == lastMonth).ToList());
        
        lViewModel.EdadPacientes = _boletaProcessor.GetEdadesPromedio(boletas);

        var top19BoletasPorEntidad = boletas
            .GroupBy(x => x.EntidadTexto)
            .OrderByDescending(g => g.Count())
            .Take(19)
            .ToDictionary(g => g.Key, g => g.Count());

        var otrasBoletasCount = boletas
            .GroupBy(x => x.EntidadTexto)
            .OrderByDescending(g => g.Count())
            .Skip(19)
            .Sum(g => g.Count());

        top19BoletasPorEntidad["Otras"] = otrasBoletasCount;

        lViewModel.BoletasPorEntidadGeneral = top19BoletasPorEntidad
            .OrderByDescending(kvp => kvp.Value)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);


        lViewModel.BoletasPorEntidadLastPeriod = boletas
            .Where(b => b.PeriodoAnio == lastYear && b.PeriodoMes == lastMonth)
            .GroupBy(x => x.EntidadTexto)
            .OrderByDescending(g => g.Count())
            .ToDictionary(g => g.Key, g => g.Count());
        


        #endregion

        #region Datos Públicos

        // Cuenta la cantidad de boletas públicas del último periodo y las asigna al ViewModel
        lViewModel.CantidadBoletasPublicas = boletas
            .Where(b => lEntidadesPublicas.Any(e => e.Codigo == b.Entidad.Codigo) &&
                b.PeriodoAnio == lastYear &&
                b.PeriodoMes == lastMonth)
            .Count();

        // Calcula el promedio de boletas del último mes público y lo asigna al ViewModel
        lViewModel.PromedioBoletaUltimoMesPublico = boletas
            .Where(b => lEntidadesPublicas.Any(e => e.Codigo == b.Entidad.Codigo) &&
                b.PeriodoAnio == lastYear &&
                b.PeriodoMes == lastMonth)
            .Select(b => b.Facturado)
            .Average();

        // Obtiene la boleta de menor valor de los últimos 3 meses públicos y la asigna al ViewModel
        lViewModel.BoletaMenorValorU3MPublico = _boletaProcessor.GetBoletaMenorValorUltimos3Meses(
            boletas.Where(b => lEntidadesPublicas.Any(e => e.Codigo == b.Entidad.Codigo) &&
                       b.PeriodoAnio == lastYear &&
                       b.PeriodoMes == lastMonth).ToList());

        // Obtiene la boleta de mayor valor de los últimos 3 meses públicos y la asigna al ViewModel
        lViewModel.BoletaMayorValorU3MPublico = _boletaProcessor.GetBoletaMayorValorUltimos3Meses(
            boletas.Where(b => lEntidadesPublicas.Any(e => e.Codigo == b.Entidad.Codigo) &&
                       b.PeriodoAnio == lastYear &&
                       b.PeriodoMes == lastMonth).ToList());

        // Obtiene las boletas del último periodo público
        var ultimoPeriodoPublico = boletas
            .Where(b => lEntidadesPublicas.Any(e => e.Codigo == b.Entidad.Codigo) &&
                b.PeriodoAnio == lastYear &&
                b.PeriodoMes == lastMonth)
            .ToList();

        // Calcula el ingreso promedio bruto del último periodo público y lo asigna al ViewModel
        lViewModel.IngresoPromedioUltimoBrutoPublico = ultimoPeriodoPublico
            .Select(b => b.Facturado)
            .Sum();

        // Obtiene los saldos históricos públicos y los asigna al ViewModel
        lViewModel.SaldosHistoricosPublicos = _boletaProcessor.GetSaldosMensuales(boletas.Where(b => lEntidadesPublicas.Any(e => e.Codigo == b.Entidad.Codigo)).ToList());

        // Ordena y asigna los ingresos mensuales públicos al ViewModel 
        lViewModel.IngresosMensualesPublico = lViewModel.SaldosHistoricosPublicos
            .OrderBy(s => DateTime.Parse(s.Periodo))
            .ToDictionary(s => DateTime.Parse(s.Periodo), s => s.ImporteFacturadoBruto); 
        
        // Obtiene y asigna la cantidad de boletas mensuales públicas al ViewModel
        lViewModel.CantidadBoletasMensualesPublico = _boletaProcessor.GetBoletasMensualesPublico(boletas.Where(b => lEntidadesPublicas.Any(e => e.Codigo == b.Entidad.Codigo)).ToList()).OrderBy(x => x.Key).TakeLast(12).ToDictionary(x => x.Key, x => x.Value);

        // Agrupa y asigna la cantidad de boletas por mutual público al ViewModel
        lViewModel.CantidadBoletasPorMutualPublico = boletas
            .Where(b => lEntidadesPublicas.Any(e => e.Codigo == b.Entidad.Codigo) &&
                b.PeriodoAnio == lastYear &&
                b.PeriodoMes == lastMonth)
            .GroupBy(b => b.Entidad.Nombre)
            .ToDictionary(g => g.Key, g => g.Count());

        // Obtiene y asigna los montos próximos a cobrar público al ViewModel
        lViewModel.MontosProximosACobrarPublico = _boletaProcessor.GetMontosPorPeriodo(boletas.Where(b => lEntidadesPublicas.Any(e => e.Codigo == b.Entidad.Codigo)).ToList(), lastYear, lastMonth);

        

        #endregion

        #region Privados
        // Cuenta la cantidad de boletas privadas del último periodo y las asigna al ViewModel
        lViewModel.CantidadBoletasPrivadas = boletas
            .Where(b => lEntidadesPrivadas.Any(e => e.Codigo == b.Entidad.Codigo) &&
                b.PeriodoAnio == lastYear &&
                b.PeriodoMes == lastMonth)
            .Count();

        // Calcula el promedio de boletas del último mes privado y lo asigna al ViewModel
        lViewModel.PromedioBoletaUltimoMesPrivados = boletas
            .Where(b => lEntidadesPrivadas.Any(e => e.Codigo == b.Entidad.Codigo) &&
                b.PeriodoAnio == lastYear &&
                b.PeriodoMes == lastMonth)
            .Select(b => b.Facturado)
            .Average();

        // Obtiene la boleta de menor valor de los últimos 3 meses privados y la asigna al ViewModel
        lViewModel.BoletaMenorValorU3MPrivados = _boletaProcessor.GetBoletaMenorValorUltimos3Meses(
            boletas.Where(b => lEntidadesPrivadas.Any(e => e.Codigo == b.Entidad.Codigo) &&
                       b.PeriodoAnio == lastYear &&
                       b.PeriodoMes == lastMonth).ToList());

        // Obtiene la boleta de mayor valor de los últimos 3 meses privados y la asigna al ViewModel
        lViewModel.BoletaMayorValorU3MPrivados = _boletaProcessor.GetBoletaMayorValorUltimos3Meses(
            boletas.Where(b => lEntidadesPrivadas.Any(e => e.Codigo == b.Entidad.Codigo) &&
                       b.PeriodoAnio == lastYear &&
                       b.PeriodoMes == lastMonth).ToList());

        // Obtiene las boletas del último periodo privado
        var ultimoPeriodoPrivado = boletas
            .Where(b => lEntidadesPrivadas.Any(e => e.Codigo == b.Entidad.Codigo) &&
                b.PeriodoAnio == lastYear &&
                b.PeriodoMes == lastMonth)
            .ToList();

        // Calcula el ingreso promedio bruto del último periodo privado y lo asigna al ViewModel
        lViewModel.IngresoPromedioUltimoPrivado = ultimoPeriodoPrivado
            .Select(b => b.Facturado)
            .Sum();

        // Obtiene los saldos históricos privados y los asigna al ViewModel
        lViewModel.SaldosHistoricosPrivados = _boletaProcessor.GetSaldosMensuales(boletas.Where(b => lEntidadesPrivadas.Any(e => e.Codigo == b.Entidad.Codigo)).ToList());

        // Ordena y asigna los ingresos mensuales privados al ViewModel 
        lViewModel.IngresosMensualesPrivados = lViewModel.SaldosHistoricosPrivados
            .OrderBy(s => DateTime.Parse(s.Periodo))
            .ToDictionary(s => DateTime.Parse(s.Periodo), s => s.ImporteFacturadoBruto); 

        // Obtiene y asigna la cantidad de boletas mensuales privadas al ViewModel
        lViewModel.CantidadBoletasMensualesPrivado = _boletaProcessor.GetBoletasMensualesPublico(boletas.Where(b => lEntidadesPrivadas.Any(e => e.Codigo == b.Entidad.Codigo)).ToList()).OrderBy(x => x.Key).TakeLast(12).ToDictionary(x => x.Key, x => x.Value);

        // Agrupa y asigna la cantidad de boletas por mutual privado al ViewModel
        lViewModel.CantidadBoletasPorMutualPrivados = boletas
            .Where(b => lEntidadesPrivadas.Any(e => e.Codigo == b.Entidad.Codigo) &&
                b.PeriodoAnio == lastYear &&
                b.PeriodoMes == lastMonth)
            .GroupBy(b => b.Entidad.Nombre)
            .OrderByDescending(g => g.Count())
            .ToDictionary(g => g.Key, g => g.Count());

        // Obtiene y asigna los montos próximos a cobrar privado al ViewModel
        lViewModel.MontosProximosACobrarPrivado = _boletaProcessor.GetMontosPorPeriodo(boletas.Where(b => lEntidadesPrivadas.Any(e => e.Codigo == b.Entidad.Codigo)).ToList(), lastYear, lastMonth);
    

        
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

    public static async Task<Dictionary<DateTime, double>> GetCotizacionDolar(List<DateTime> Fechas, IWebHostEnvironment hostingEnvironment)
    {
        HttpClient client = new HttpClient();
        var cotizaciones = new Dictionary<DateTime, double>();

        // Ruta del archivo JSON
        var jsonFilePath = Path.Combine(hostingEnvironment.ContentRootPath, "Files", "cotizaciones.json");

        // Leer el contenido existente del archivo JSON si existe
        var existingCotizaciones = new Dictionary<DateTime, double>();
        if (System.IO.File.Exists(jsonFilePath))
        {
            var existingContent = await System.IO.File.ReadAllTextAsync(jsonFilePath);
            existingCotizaciones = JsonConvert.DeserializeObject<Dictionary<DateTime, double>>(existingContent);
        }

        foreach (var fecha in Fechas)
        {
            if (existingCotizaciones.ContainsKey(fecha))
            {
            cotizaciones[fecha] = existingCotizaciones[fecha];
            continue;
            }
            try
            {
                // Formatea la fecha en el formato requerido por la API
                string formattedDate = fecha.ToString("yyyy-MM-dd");

                // URL de la API para obtener los valores más recientes del dólar
                string url = $"https://api.bluelytics.com.ar/v2/historical?day={formattedDate}";
                HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode(); // Verifica si la solicitud fue exitosa

                // Lee el contenido de la respuesta en formato JSON
                string responseBody = await response.Content.ReadAsStringAsync();

                // Deserializa la respuesta JSON en un objeto dinámico
                dynamic jsonResponse = JsonConvert.DeserializeObject(responseBody);

                // Extrae el valor promedio del dólar blue
                double valueAvgBlue = jsonResponse.blue.value_avg;

                // Guarda el valor en el diccionario
                cotizaciones[fecha] = valueAvgBlue;
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"\nExcepción para la fecha {fecha.ToString("yyyy-MM-dd")}: " + e.Message);
            }
        }

        // Ruta del archivo JSON
        var jsonFilePathNew = Path.Combine(hostingEnvironment.ContentRootPath, "Files", "cotizaciones.json");

        // Leer el contenido existente del archivo JSON si existe
        if (System.IO.File.Exists(jsonFilePath))
        {
            var existingContentNew = await System.IO.File.ReadAllTextAsync(jsonFilePathNew);
            var existingCotizacionesNew = JsonConvert.DeserializeObject<Dictionary<DateTime, double>>(existingContentNew);

            // Agregar solo los registros nuevos
            foreach (var cotizacion in cotizaciones)
            {
                if (!existingCotizacionesNew.ContainsKey(cotizacion.Key))
                {
                    existingCotizacionesNew[cotizacion.Key] = cotizacion.Value;
                }
            }

            // Actualizar el diccionario de cotizaciones con los registros existentes
            cotizaciones = existingCotizacionesNew;
        }

        // Guardar las cotizaciones en el archivo JSON
        var jsonContent = JsonConvert.SerializeObject(cotizaciones, Formatting.Indented);
        await System.IO.File.WriteAllTextAsync(jsonFilePath, jsonContent);

        return cotizaciones;
    }

}

