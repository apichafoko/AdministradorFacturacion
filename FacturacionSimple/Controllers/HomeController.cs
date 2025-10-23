using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using FacturacionSimple.Models;
using FacturacionSimple.Helpers;
using Newtonsoft.Json;
using FacturacionSimple.Helpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using AspNetCoreGeneratedDocument;

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
            SaldosHistoricosPrivados = [],
            FacturacionPorCirujanoPrivados = [],
            PeriodosDisponibles = [],
            SelectedPeriodVM = new SelectedPeriodViewModel(),
        };

        lViewModel.SelectedPeriodVM.BoletasPorEntidad = [];
        return View(lViewModel);
    }
    [HttpPost]
    [RequestFormLimits(MultipartBodyLengthLimit = 104857600)] // 100 MB
    [RequestSizeLimit(104857600)] // 100 MB
    public async Task<IActionResult> Index(IFormFile file)
    {
        var lViewModel = new IndexViewModel();
        lViewModel.ListadoBoletas = new List<Boleta>();
        var filePath = string.Empty;

        try
        {
            if (file == null || file.Length == 0)
            {
                ViewBag.Message = "Por favor seleccioná un archivo.";
                return View();
            }

            // Validar tamaño del archivo
            if (file.Length > 104857600) // 100 MB
            {
                ViewBag.Message = "El archivo es demasiado grande. El tamaño máximo es 100 MB.";
                return View();
            }

            var uploadsFolder = Path.Combine(_hostingEnvironment.ContentRootPath, "Files");

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var randomFileName = Path.GetRandomFileName() + Path.GetExtension(file.FileName);
            filePath = Path.Combine(uploadsFolder, randomFileName);

            // Usar un buffer más grande para mejor rendimiento con archivos grandes
            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 81920, useAsync: true))
            {
                await file.CopyToAsync(stream);
            }

            _logger.LogInformation($"Archivo subido: {file.FileName} ({file.Length} bytes)");

            // Llamar al procesador de boletas para leer y procesar el archivo
            var boletas = await _boletaProcessor.ProcesarArchivo(filePath);
        

            // Obtiene las entidades a partir de las boletas procesadas
            var lEntidades = _boletaProcessor.GetEntidades(boletas);

            // Obtiene las entidades privadas
            var lEntidadesPrivadas = _Entidades.GetEntidadesPrivadas(lEntidades);

            // Obtiene las entidades públicas
            var lEntidadesPublicas = _Entidades.GetEntidadesPublicas(lEntidades);



            // Obtiene el último periodo de las boletas
            var lastPeriod = boletas
                .Where(b => lEntidadesPrivadas.Any(e => e.Codigo == b.Entidad.Codigo))
                .OrderByDescending(s => DateTime.Parse(s.Periodo))
                .FirstOrDefault();

            if (lastPeriod == null)
            {

                lastPeriod = boletas
                .Where(b => lEntidadesPublicas.Any(e => e.Codigo == b.Entidad.Codigo))
                .OrderByDescending(s => DateTime.Parse(s.Periodo))
                .FirstOrDefault(); 
            }

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

            var lIngresosTotalesUSD = _boletaProcessor.GetFacturacionenUSD(lSaldosTotales, lCotizaciones);

            // Ordena y asigna los ingresos mensuales públicos al ViewModel 
            lViewModel.IngresosTotalesUSD = lIngresosTotalesUSD;
            lViewModel.CotizacionesUSD = lCotizaciones;
            lViewModel.CotizacionUSDLast = lCotizaciones.OrderByDescending(x => x.Key).First().Value;

            var lBoletasPrivadasLastPeriod = boletas
                .Where(b => lEntidadesPrivadas.Any(e => e.Codigo == b.Entidad.Codigo) &&
                    b.PeriodoAnio == lastYear &&
                    b.PeriodoMes == lastMonth);

            var lBoletasPublicasLastPeriod = boletas
                .Where(b => lEntidadesPublicas.Any(e => e.Codigo == b.Entidad.Codigo) &&
                    b.PeriodoAnio == lastYear &&
                    b.PeriodoMes == lastMonth);

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


            if (lBoletasPublicasLastPeriod.Count() > 0)
            {
                lViewModel.CantidadBoletasDiaPublico = _boletaProcessor.CantidadBoletasDiaSemana(
                    boletas.Where(b => lEntidadesPublicas.Any(e => e.Codigo == b.Entidad.Codigo) &&
                           b.PeriodoAnio == lastYear &&
                           b.PeriodoMes == lastMonth).ToList());
            }

            if (lBoletasPrivadasLastPeriod.Count() > 0)
            {
                lViewModel.CantidadBoletasDiaPrivado = _boletaProcessor.CantidadBoletasDiaSemana(
                    boletas.Where(b => lEntidadesPrivadas.Any(e => e.Codigo == b.Entidad.Codigo) &&
                               b.PeriodoAnio == lastYear &&
                               b.PeriodoMes == lastMonth).ToList());
                    

                lViewModel.EdadPacientes = _boletaProcessor.GetEdadesPromedio(boletas);
            }

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

            lViewModel.ListadoBoletasPrivadas = boletas.Where(b => lEntidadesPrivadas.Any(e => e.Codigo == b.Entidad.Codigo)).ToList();
            lViewModel.ListadoBoletasPublicas = boletas.Where(b => lEntidadesPublicas.Any(e => e.Codigo == b.Entidad.Codigo)).ToList();
            
            #endregion

             #region ParcialesyDebitos
            if (lViewModel.ListadoBoletasPrivadas.Count()> 0)
            {
                lViewModel.ListadoBoletasParciales = _boletaProcessor.GetBoletasParciales(lViewModel.ListadoBoletasPrivadas);
                lViewModel.ListadoBoletasDebitadas = _boletaProcessor.GetBoletasConDebitos(lViewModel.ListadoBoletasPrivadas);
                lViewModel.ListadoBoletasDebitadasLastPeriod = _boletaProcessor.GetBoletasConDebitos(lBoletasPrivadasLastPeriod.ToList());
                lViewModel.CantidadBoletasPagosParciales = lViewModel.ListadoBoletasParciales.Count();
            }
            #endregion

            #region Datos Públicos

            if (lBoletasPublicasLastPeriod.Count() > 0)
            {
            // Cuenta la cantidad de boletas públicas del último periodo y las asigna al ViewModel
                lViewModel.CantidadBoletasPublicas = lBoletasPublicasLastPeriod.Count();

                // Calcula el promedio de boletas del último mes público y lo asigna al ViewModel
                lViewModel.PromedioBoletaUltimoMesPublico = lBoletasPublicasLastPeriod
                    .Select(b => b.Facturado)
                    .Average();

                // Obtiene la boleta de menor valor de los últimos 3 meses públicos y la asigna al ViewModel
                lViewModel.BoletaMenorValorU3MPublico = _boletaProcessor.GetBoletaMenorValorUltimos3Meses(
                    lBoletasPublicasLastPeriod.ToList());

                // Obtiene la boleta de mayor valor de los últimos 3 meses públicos y la asigna al ViewModel
                lViewModel.BoletaMayorValorU3MPublico = _boletaProcessor.GetBoletaMayorValorUltimos3Meses(
                    lBoletasPublicasLastPeriod.ToList());

                // Calcula el ingreso promedio bruto del último periodo público y lo asigna al ViewModel
                lViewModel.IngresoPromedioUltimoBrutoPublico = lBoletasPublicasLastPeriod
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
            }


            #endregion

            #region Privados


            // Cuenta la cantidad de boletas privadas del último periodo y las asigna al ViewModel

            if (lBoletasPrivadasLastPeriod.Count() > 0)
            {

                lViewModel.CantidadBoletasPrivadas = lBoletasPrivadasLastPeriod.Count();

                // Calcula el promedio de boletas del último mes privado y lo asigna al ViewModel
                lViewModel.PromedioBoletaUltimoMesPrivados = lBoletasPrivadasLastPeriod
                    .Select(b => b.Facturado)
                    .Average();

                // Obtiene la boleta de menor valor de los últimos 3 meses privados y la asigna al ViewModel
                lViewModel.BoletaMenorValorU3MPrivados = _boletaProcessor.GetBoletaMenorValorUltimos3Meses(
                    lBoletasPrivadasLastPeriod);

                // Obtiene la boleta de mayor valor de los últimos 3 meses privados y la asigna al ViewModel
                lViewModel.BoletaMayorValorU3MPrivados = _boletaProcessor.GetBoletaMayorValorUltimos3Meses(
                    lBoletasPrivadasLastPeriod);

                // Calcula el ingreso promedio bruto del último periodo privado y lo asigna al ViewModel
                lViewModel.IngresoPromedioUltimoPrivado = lBoletasPrivadasLastPeriod
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
                lViewModel.CantidadBoletasPorMutualPrivados =
                    lBoletasPrivadasLastPeriod
                    .GroupBy(b => b.Entidad.Nombre)
                    .OrderByDescending(g => g.Count())
                    .ToDictionary(g => g.Key, g => g.Count());

                // Obtiene y asigna los montos próximos a cobrar privado al ViewModel
                lViewModel.MontosProximosACobrarPrivado = _boletaProcessor.GetMontosPorPeriodo(boletas.Where(b => lEntidadesPrivadas.Any(e => e.Codigo == b.Entidad.Codigo)).ToList(), lastYear, lastMonth);

                lViewModel.CantidadBoletasPorHospitalPrivados = _boletaProcessor.GetCantidadBoletasPorHospital(lBoletasPrivadasLastPeriod.ToList());

                lViewModel.FacturacionPorCirujanoPrivados = _boletaProcessor.GetFacturacionPorCirujano(
                    lBoletasPrivadasLastPeriod.ToList());


                lViewModel.CantidadBoletasSemanalesPrivado = _boletaProcessor.CantidadBoletasDiaSemanales(
                    lBoletasPrivadasLastPeriod.ToList(),lastMonth,lastYear);
            }
            #endregion

           
           #region BoletasPorCirujanos
           var lCirujanosHelper = new CirujanoHelper();
           lViewModel.CirujanosDisponibles = new List<string> { "" }.Concat(lCirujanosHelper.GetCirujanosDisponibles(boletas)).ToList();
           #endregion

           #region BoletasPorHospital
           var lHospitalHelper = new HospitalHelper();
           lViewModel.HospitalesDisponibles = new List<string> { "" }.Concat(lHospitalHelper.GetHospitalesDisponibles(boletas)).ToList();
           #endregion

            ViewBag.Message = "Archivo procesado exitosamente.";

            // Incrementar el contador de usuarios
            var contadorFilePath = Path.Combine(_hostingEnvironment.ContentRootPath, "Files", "Contador.json");
            var contadorData = new Dictionary<string, int>();

            if (System.IO.File.Exists(contadorFilePath))
            {
                var existingContent = await System.IO.File.ReadAllTextAsync(contadorFilePath);
                contadorData = JsonConvert.DeserializeObject<Dictionary<string, int>>(existingContent);
            }

            var today = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");

            if (contadorData.ContainsKey(today))
            {
                contadorData[today]++;
            }
            else
            {
                contadorData[today] = 1;
            }

            var jsonContent = JsonConvert.SerializeObject(contadorData, Formatting.Indented);
            await System.IO.File.WriteAllTextAsync(contadorFilePath, jsonContent);

            // Eliminar el archivo después de procesarlo
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
            lViewModel.PeriodosDisponibles = _boletaProcessor.GetPeriodosPrevios(boletas.Where(b => lEntidadesPrivadas.Any(e => e.Codigo == b.Entidad.Codigo)).ToList(),lastYear,lastMonth);
            if (lViewModel.PeriodosDisponibles.Count() ==  0)
            {
                lViewModel.PeriodosDisponibles = _boletaProcessor.GetPeriodosPrevios(boletas.Where(b => lEntidadesPublicas.Any(e => e.Codigo == b.Entidad.Codigo)).ToList(),lastYear,lastMonth);
            }
            
            lViewModel.PeriodoSeleccionado = lastYear + "-" + lastPeriod;

            lViewModel.CantidadBoletasLastPeriod = lBoletasPrivadasLastPeriod.Concat(lBoletasPublicasLastPeriod).Count();
            lViewModel.IngresosTotalesLastPeriod = lBoletasPrivadasLastPeriod.Concat(lBoletasPublicasLastPeriod).Sum(x => x.Facturado);
            lViewModel.ListadoBoletasLastPeriod = lBoletasPrivadasLastPeriod.Concat(lBoletasPublicasLastPeriod).ToList();


            var lIngresosTotalesUSDLastPeriod = _boletaProcessor.GetFacturacionenUSD(lSaldosTotales, lCotizaciones);

            // Ordena y asigna los ingresos mensuales públicos al ViewModel 
            lViewModel.IngresosTotalesUSDLastPeriod = lIngresosTotalesUSD.Where(x=>x.Key == new DateTime(lastYear,lastMonth,1)).FirstOrDefault().Value;
            lViewModel.IngresosMensualesPrivadoLastPeriod = lBoletasPrivadasLastPeriod.Sum(x=>x.Facturado);
            lViewModel.IngresosMensualesPublicoLastPeriod = lBoletasPublicasLastPeriod.Sum(x=>x.Facturado);


            var lMejorPeriodo = _boletaProcessor.GetMejorPeriodo(lIngresosTotalesUSD);
            lViewModel.MejorPeriodo = lMejorPeriodo.Keys.First();
            lViewModel.ImporteMejorPeriodo = lMejorPeriodo.Values.First();
            lViewModel.DiferenciaMejorPeriodoTotal = _boletaProcessor.GetDiferenciaMejorPeriodo(lMejorPeriodo.Values.First(), lViewModel.IngresosTotalesUSDLastPeriod);


        }
        catch (ExcelFormatException ex)
        {
            // Capturar errores de formato de Excel
            _logger.LogWarning(ex, "Archivo Excel con formato incorrecto");

            // Construir mensaje detallado para el usuario con TODAS las celdas
            var mensajeDetallado = $"El archivo Excel tiene {ex.CeldasProblematicas.Count} celda(s) con formato incorrecto:\n\n";
            mensajeDetallado += string.Join("\n", ex.CeldasProblematicas);
            mensajeDetallado += "\n\nPor favor, corrija el formato en Excel antes de volver a subir el archivo.";

            TempData["ErrorFormatoCeldas"] = mensajeDetallado;
            TempData["CeldasProblematicas"] = JsonConvert.SerializeObject(ex.CeldasProblematicas);

            // Eliminar el archivo con formato incorrecto
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            // Inicializar ViewModel para evitar errores en la vista
            lViewModel.SelectedPeriodVM = new SelectedPeriodViewModel();
            lViewModel.SelectedPeriodVM.BoletasPorEntidad = new Dictionary<string, int>();
            lViewModel.SaldosHistoricosPublicos = new List<SaldosMensuales>();
            lViewModel.SaldosHistoricosPrivados = new List<SaldosMensuales>();
            lViewModel.FacturacionPorCirujanoPrivados = new List<ItemCirujanoDTO>();
            lViewModel.PeriodosDisponibles = new List<string>();

            return View(lViewModel);
        }
        catch (Exception ex)
        {
            // Capturar cualquier excepción y registrar el error internamente si es necesario
            _logger.LogError(ex, "Error al procesar el archivo");

            // Mostrar un mensaje genérico al usuario sin exponer el detalle del error
            ViewBag.Message = "Ocurrió un error al procesar el archivo. Por favor, intente nuevamente.";

            // Eliminar el archivo después de procesarlo
            if (System.IO.File.Exists(filePath))
            {
                //System.IO.File.Delete(filePath);
            }
        }

        lViewModel.SelectedPeriodVM = new SelectedPeriodViewModel();
        lViewModel.SelectedPeriodVM.BoletasPorEntidad = new Dictionary<string, int>();
        lViewModel.ListadoBoletasCirujanoSeleccionado = new List<Boleta>();
        lViewModel.BoletasPorPeriodoCirujanoSeleccionado = new List<ResumenPeriodoDTO>();

        
        // Eliminar los objetos de Session
        
        SessionObjects.VM = new IndexViewModel();

        SessionObjects.VM = lViewModel;

        return View(lViewModel);

    }


    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    public static async Task<Dictionary<DateTime, double>> GetCotizacionDolarOld(List<DateTime> Fechas, IWebHostEnvironment hostingEnvironment)
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
            if (existingCotizaciones != null && existingCotizaciones.ContainsKey(fecha))
            {
                cotizaciones[fecha] = existingCotizaciones[fecha];
                continue;
            }
            try
            {
                // Formatea la fecha en el formato requerido por la API
                string formattedDate = fecha.ToString("yyyy-MM");

                // URL de la API para obtener los valores más recientes del dólar
                string url = $"https://api.bluelytics.com.ar/v2/evolution.json";
                HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode(); // Verifica si la solicitud fue exitosa

                // Lee el contenido de la respuesta en formato JSON
                string responseBody = await response.Content.ReadAsStringAsync();

                // Deserializa la respuesta JSON en un objeto dinámico
                var jsonResponse = JsonConvert.DeserializeObject<List<JsonDTO>>(responseBody);

                var valoresUsdJson = jsonResponse.Where(x => x.date.Month == fecha.Month && x.date.Year == fecha.Year && x.source == "Blue").ToList();

                var valoresMes = valoresUsdJson.Select(x => (x.value_buy + x.value_sell) / 2).ToList();

                double valueAvgBlue = Math.Round(valoresMes.Average(), 2);

                // Guarda el valor en el diccionario
                cotizaciones[fecha] = valueAvgBlue;

                // Actualiza el archivo JSON con el nuevo valor
                if (existingCotizaciones == null)
                {
                    existingCotizaciones = new Dictionary<DateTime, double>();
                }
                existingCotizaciones[fecha] = valueAvgBlue;
                var jsonContent = JsonConvert.SerializeObject(existingCotizaciones, Formatting.Indented);
                await System.IO.File.WriteAllTextAsync(jsonFilePath, jsonContent);
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"\nExcepción para la fecha {fecha.ToString("yyyy-MM")}: " + e.Message);
            }
        }

        return cotizaciones;
    }




    // Acción para manejar el cambio de período
    [HttpPost]
    public async Task<IActionResult> ActualizarVistasPorPeriodo(string periodo)
    {
            var lBoletasFiltradasPrivadas = _boletaProcessor.GetBoletasPorPeriodo(SessionObjects.VM.ListadoBoletasPrivadas,periodo);
            var lBoletasFiltradasPublicas = _boletaProcessor.GetBoletasPorPeriodo(SessionObjects.VM.ListadoBoletasPublicas,periodo);

            var periodParts = periodo.Split('-');
            var lastYear = int.Parse(periodParts[0]);
            var lastMonth = int.Parse(periodParts[1]);
            #region Privados

            // Cuenta la cantidad de boletas privadas del último periodo y las asigna al ViewModel

            if (lBoletasFiltradasPrivadas.Count() > 0 || lBoletasFiltradasPublicas.Count() > 0)
            {

                SessionObjects.VM.SelectedMonth = lastMonth;
                SessionObjects.VM.SelectedYear = lastYear;

                SessionObjects.VM.SelectedPeriodVM.CantidadBoletasTotales = lBoletasFiltradasPrivadas.Count() + lBoletasFiltradasPublicas.Count();;
                SessionObjects.VM.SelectedPeriodVM.IngresosTotales = lBoletasFiltradasPrivadas
                    .Select(b => b.Facturado)
                    .Sum() + lBoletasFiltradasPublicas
                    .Select(b => b.Facturado)
                    .Sum();

                var fechasPrimerDiaMes = new List<DateTime>();
                
                if (lBoletasFiltradasPrivadas.Count() > 0)
                {
                    SessionObjects.VM.SelectedPeriodVM.CantidadBoletasPrivadas = lBoletasFiltradasPrivadas.Count();

                    // Calcula el promedio de boletas del último mes privado y lo asigna al ViewModel
                    SessionObjects.VM.SelectedPeriodVM.PromedioBoletaPrivados = lBoletasFiltradasPrivadas
                        .Select(b => b.Facturado)
                        .Average();

                         // Obtiene la boleta de menor valor de los últimos 3 meses privados y la asigna al ViewModel
                    SessionObjects.VM.SelectedPeriodVM.BoletaMenorValorPrivados = _boletaProcessor.GetBoletaMenorValorUltimos3Meses(
                    lBoletasFiltradasPrivadas);

                    SessionObjects.VM.SelectedPeriodVM.BoletaMayorValorPrivados = _boletaProcessor.GetBoletaMayorValorUltimos3Meses(
                    lBoletasFiltradasPrivadas);

                    // Calcula el ingreso promedio bruto del último periodo privado y lo asigna al ViewModel
                    SessionObjects.VM.SelectedPeriodVM.IngresoPromedioPrivado = lBoletasFiltradasPrivadas
                        .Select(b => b.Facturado)
                        .Sum();

                        SessionObjects.VM.SelectedPeriodVM.CantidadBoletasPorMutualPrivados =
                    lBoletasFiltradasPrivadas
                    .GroupBy(b => b.Entidad.Nombre)
                    .OrderByDescending(g => g.Count())
                    .ToDictionary(g => g.Key, g => g.Count());

                    SessionObjects.VM.SelectedPeriodVM.FacturacionPorCirujanoPrivados = _boletaProcessor.GetFacturacionPorCirujano(
                    lBoletasFiltradasPrivadas.ToList());


                    fechasPrimerDiaMes = lBoletasFiltradasPrivadas
                        .Select(b => new DateTime(b.PeriodoAnio, b.PeriodoMes, 1))
                        .Distinct()
                        .OrderBy(d => d)
                        .ToList();

                         SessionObjects.VM.SelectedPeriodVM.CantidadBoletasDiaPrivado = _boletaProcessor.CantidadBoletasDiaSemana(
                        lBoletasFiltradasPrivadas);

                }

                if (lBoletasFiltradasPublicas.Count() > 0)
                {
                    SessionObjects.VM.SelectedPeriodVM.CantidadBoletasPublicas = lBoletasFiltradasPublicas.Count();

                    SessionObjects.VM.SelectedPeriodVM.PromedioBoletaPublico = lBoletasFiltradasPublicas
                    .Select(b => b.Facturado)
                    .Average();

                    SessionObjects.VM.SelectedPeriodVM.BoletaMenorValorPublico = _boletaProcessor.GetBoletaMenorValorUltimos3Meses(
                    lBoletasFiltradasPublicas);

                    SessionObjects.VM.SelectedPeriodVM.BoletaMayorValorPublico = _boletaProcessor.GetBoletaMayorValorUltimos3Meses(
                    lBoletasFiltradasPublicas);

                    SessionObjects.VM.SelectedPeriodVM.IngresoPromedioBrutoPublico = lBoletasFiltradasPublicas
                    .Select(b => b.Facturado)
                    .Sum();

                    SessionObjects.VM.SelectedPeriodVM.CantidadBoletasPorMutualPublico =
                    lBoletasFiltradasPublicas
                    .GroupBy(b => b.Entidad.Nombre)
                    .OrderByDescending(g => g.Count())
                    .ToDictionary(g => g.Key, g => g.Count());

                    if (lBoletasFiltradasPrivadas.Count() == 0)
                    {
                        fechasPrimerDiaMes = lBoletasFiltradasPublicas
                        .Select(b => new DateTime(b.PeriodoAnio, b.PeriodoMes, 1))
                        .Distinct()
                        .OrderBy(d => d)
                        .ToList();
                    }

                    SessionObjects.VM.SelectedPeriodVM.CantidadBoletasDiaPublico = _boletaProcessor.CantidadBoletasDiaSemana(
                        lBoletasFiltradasPublicas);
                }

            
                SessionObjects.VM.SelectedPeriodVM.CantidadBoletasPorHospital = _boletaProcessor.GetCantidadBoletasPorHospital(lBoletasFiltradasPrivadas.Concat(lBoletasFiltradasPublicas).ToList());

                
                SessionObjects.VM.SelectedPeriodVM.BoletasPorEntidad = 
                    lBoletasFiltradasPrivadas.Concat(lBoletasFiltradasPublicas)
                    .GroupBy(x => x.EntidadTexto)
                    .OrderByDescending(g => g.Count())
                    .ToDictionary(g => g.Key, g => g.Count());

                #region CotizacionDolar

                    var lCotizaciones = await GetCotizacionDolar(fechasPrimerDiaMes, _hostingEnvironment).ConfigureAwait(false);


                    var lSaldosTotales = _boletaProcessor.GetSaldosMensuales(lBoletasFiltradasPrivadas.Concat(lBoletasFiltradasPublicas).ToList());

                    var lIngresosTotalesUSD = _boletaProcessor.GetFacturacionenUSD(lSaldosTotales, lCotizaciones);

                    // Ordena y asigna los ingresos mensuales públicos al ViewModel 
                    SessionObjects.VM.SelectedPeriodVM.IngresosTotalesUSD = lIngresosTotalesUSD.First().Value;
                    SessionObjects.VM.SelectedPeriodVM.CotizacionUSD = lCotizaciones.Where(x=>x.Key == fechasPrimerDiaMes.Last()).First().Value;


                    // Cuenta la cantidad de boletas por día de la semana del último periodo y las asigna al ViewModel
                    SessionObjects.VM.SelectedPeriodVM.CantidadBoletasDia = _boletaProcessor.CantidadBoletasDiaSemana(lBoletasFiltradasPrivadas.Concat(lBoletasFiltradasPublicas).ToList());

                    
                    SessionObjects.VM.SelectedPeriodVM.ListadoBoletas = lBoletasFiltradasPrivadas.Concat(lBoletasFiltradasPublicas).ToList();
                                        
                    SessionObjects.VM.SelectedPeriodVM.DiferenciaMejorPeriodoTotal = _boletaProcessor.GetDiferenciaMejorPeriodo(SessionObjects.VM.ImporteMejorPeriodo, lIngresosTotalesUSD.First().Value);

                #endregion


                #region PercialesYDebitos
                SessionObjects.VM.SelectedPeriodVM.ListadoBoletasParciales = _boletaProcessor.GetBoletasParciales(lBoletasFiltradasPrivadas.ToList());
                SessionObjects.VM.SelectedPeriodVM.ListadoBoletasDebitadas = _boletaProcessor.GetBoletasConDebitos(lBoletasFiltradasPrivadas.ToList());
                
                #endregion
                }
            #endregion  

           return PartialView("Partial_DatosPorPeriodo", SessionObjects.VM);

    }

    

        // Acción para manejar el cambio de período
    [HttpPost]
    public async Task<IActionResult> ActualizarVistasPorCirujano(string cirujano)
    {
            var lBoletasFiltradas = _boletaProcessor.GetBoletasPorCirujano(SessionObjects.VM.ListadoBoletas,cirujano).ToList();

        
            // Cuenta la cantidad de boletas privadas del último periodo y las asigna al ViewModel

            if (lBoletasFiltradas.Count() > 0)
            {

                SessionObjects.VM.CirujanoSeleccionado = cirujano;
                SessionObjects.VM.ListadoBoletasCirujanoSeleccionado = lBoletasFiltradas;
                
            
                SessionObjects.VM.SelectedPeriodVM.CantidadBoletasPorHospital = _boletaProcessor.GetCantidadBoletasPorHospital(lBoletasFiltradas);

                
                SessionObjects.VM.SelectedPeriodVM.BoletasPorEntidad = 
                    lBoletasFiltradas
                    .GroupBy(x => x.EntidadTexto)
                    .OrderByDescending(g => g.Count())
                    .ToDictionary(g => g.Key, g => g.Count());
                }

                var lCirujanosHelper = new CirujanoHelper();

                SessionObjects.VM.BoletasPorPeriodoCirujanoSeleccionado = lCirujanosHelper.GetResumenPorPeriodo(lBoletasFiltradas);
            

           return PartialView("Partial_DatosPorCirujano", SessionObjects.VM);

    }


    [HttpPost]
    public async Task<IActionResult> ActualizarVistasPorHospital(string hospital)
    {
            var lBoletasFiltradas = _boletaProcessor.GetBoletasPorHospital(SessionObjects.VM.ListadoBoletas,hospital).ToList();

        
            // Cuenta la cantidad de boletas privadas del último periodo y las asigna al ViewModel

            if (lBoletasFiltradas.Count() > 0)
            {

                SessionObjects.VM.HospitalSeleccionado = hospital;
                SessionObjects.VM.ListadoBoletasHospitalSeleccionado = lBoletasFiltradas;
                
            
                SessionObjects.VM.SelectedPeriodVM.CantidadBoletasPorHospital = _boletaProcessor.GetCantidadBoletasPorCirujano(lBoletasFiltradas);

                
                SessionObjects.VM.SelectedPeriodVM.BoletasPorEntidad = 
                    lBoletasFiltradas
                    .GroupBy(x => x.EntidadTexto)
                    .OrderByDescending(g => g.Count())
                    .ToDictionary(g => g.Key ?? "Desconocido", g => g.Count());
                }

                var lHospitalHelper = new HospitalHelper();

                SessionObjects.VM.BoletasPorPeriodoHospitalSeleccionado = lHospitalHelper.GetResumenPorPeriodo(lBoletasFiltradas);
            

           return PartialView("Partial_DatosPorHospital", SessionObjects.VM);

    }
    
// Método auxiliar para renderizar vistas parciales como cadenas de texto
    private string RenderRazorViewToString(Controller controller, string viewName, object model)
    {
        controller.ViewData.Model = model;
        using (var sw = new StringWriter())
        {
            var viewEngine = controller.HttpContext.RequestServices.GetService(typeof(ICompositeViewEngine)) as ICompositeViewEngine;
            var viewResult = viewEngine.FindView(controller.ControllerContext, viewName, false);
            if (!viewResult.Success)
            {
                throw new InvalidOperationException($"No se pudo encontrar la vista parcial '{viewName}'");
            }
            var viewContext = new ViewContext(controller.ControllerContext, viewResult.View, controller.ViewData, controller.TempData, sw, new HtmlHelperOptions());
            viewResult.View.RenderAsync(viewContext).GetAwaiter().GetResult();
            return sw.GetStringBuilder().ToString();
        }
    }


}

