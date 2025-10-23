using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using ExcelDataReader;
using FacturacionSimple.Models;

using Microsoft.AspNetCore.SignalR;

namespace FacturacionSimple.Helpers
{
    /// <summary>
    /// Excepción personalizada para errores de formato en celdas de Excel
    /// </summary>
    public class ExcelFormatException : Exception
    {
        public List<string> CeldasProblematicas { get; set; } = new List<string>();

        public ExcelFormatException(string message, List<string> celdas) : base(message)
        {
            CeldasProblematicas = celdas;
        }
    }

    public class BoletaProcessor
    {
        private readonly IHubContext<ProgressHub> _hubContext;

        public BoletaProcessor(IHubContext<ProgressHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task<List<Boleta>> ProcesarArchivo(string filePath)
        {
            try
            {
                // Leer las boletas de manera eficiente
                var boletas = await Task.Run(() => LeerBoletasDesdeArchivo(filePath));

                // Enviar notificación de progreso final
                await _hubContext.Clients.All.SendAsync("UpdateProgress", 100);

                return boletas;
            }
            catch (Exception ex)
            {
                // Log del error y re-lanzar
                throw;
            }
        }

        private List<Boleta> LeerBoletasDesdeArchivo(string filePath)
        {
            // Pre-allocar capacidad estimada para mejor performance con archivos grandes
            var boletas = new List<Boleta>(30000);
            int rowCount = 0;
            int progressInterval = 1000; // Reportar progreso cada 1000 filas
            int lastProgressReported = 0;
            var celdasProblematicas = new List<string>(); // Lista de celdas con formato incorrecto

            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read))
            {
                using var reader = ExcelReaderFactory.CreateReader(stream);
                {
                    // Leer el archivo fila por fila
                    while (reader.Read())
                    {
                        rowCount++;

                        if (reader.Depth > 2) // Saltar la cabecera
                        {
                            // Omitir filas que contengan la cabecera completa o cualquiera de sus columnas
                            var rawValues = Enumerable.Range(0, reader.FieldCount)
                                .Select(i => reader.GetValue(i)?.ToString()?.Trim() ?? string.Empty)
                                .ToArray();

                            string[] headerCandidates = new[]
                            {
                                "Nro / Nombre del Socio","Boleta","Nro / Nombre de Mutual","Fec.Boleta",
                                "Periodo","Nro / Nombre de Hospital","Nombre de Paciente","NumAfiliado",
                                "Edad","Cirujano","Facturado","Cobrado","Debitado"
                            };

                            string Normalize(string s) =>
                                new string((s ?? string.Empty)
                                    .Where(c => !char.IsWhiteSpace(c) && c != '.' && c != '/' && c != '-' && c != '\t')
                                    .ToArray())
                                .ToLowerInvariant();

                            var normalizedRow = string.Concat(rawValues.Select(Normalize));
                            var normalizedHeaders = headerCandidates.Select(Normalize).ToArray();

                            // Si la fila contiene cualquiera de las cabeceras normalizadas, saltarla
                            if (normalizedHeaders.Any(h => !string.IsNullOrEmpty(h) && normalizedRow.Contains(h)))
                            {
                                continue;
                            }

                            // Verificar si la fila está completamente vacía
                            bool isRowEmpty = rawValues.All(string.IsNullOrWhiteSpace);
                            if (isRowEmpty)
                            {
                                continue; // Saltar filas vacías
                            }

                            // Verificar si las columnas críticas tienen valores
                            var numeroBoleta = reader.GetValue(1);
                            var fechaBoletaValue = reader.GetValue(3);
                            var periodo = reader.GetValue(4);

                            // Si faltan datos críticos, saltar la fila
                            if (numeroBoleta == null || fechaBoletaValue == null || periodo == null)
                            {
                                continue;
                            }

                            var lBoleta = new Boleta();

                            lBoleta.NumeroBoleta = Convert.ToInt64(numeroBoleta);
                            lBoleta.EntidadTexto = reader.GetValue(2)?.ToString();
                            lBoleta.Periodo = reader.GetValue(4)?.ToString();
                            lBoleta.PeriodoMes = Convert.ToDateTime(lBoleta.Periodo).Month;
                            lBoleta.PeriodoAnio = Convert.ToDateTime(lBoleta.Periodo).Year;

                            lBoleta.Hospital = reader.GetValue(5)?.ToString();
                            lBoleta.Edad = SafeParseInt(reader.GetValue(8), rowCount, "I", ref celdasProblematicas);
                            lBoleta.Cirujano = reader.GetValue(9)?.ToString();
                            lBoleta.Facturado = SafeParseDouble(reader.GetValue(10), rowCount, "K", ref celdasProblematicas);
                            lBoleta.Cobrado = SafeParseDouble(reader.GetValue(11), rowCount, "L", ref celdasProblematicas);
                            lBoleta.Debitado = SafeParseDouble(reader.GetValue(12), rowCount, "M", ref celdasProblematicas);
                            lBoleta.Saldo = lBoleta.Facturado - (lBoleta.Cobrado + lBoleta.Debitado);

                            var parts = lBoleta.EntidadTexto?.Split('/') ?? Array.Empty<string>();
                            var codigo = parts.Length > 0 ? parts[0] : string.Empty;
                            var texto = parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : string.Empty;

                            var lEntidad = new Entidad
                            {
                                Codigo = int.TryParse(codigo.TrimStart('0', '2'), out var codigoInt) ? codigoInt : 0,
                                Nombre = texto
                            };

                            lBoleta.Entidad = lEntidad;
                            lBoleta.EntidadCodigo = lEntidad.Codigo;
                            lBoleta.EntidadTexto = lEntidad.Nombre;

                            // Intentar parsear la fecha de forma flexible
                            DateTime fechaBoleta;
                            var fechaParts = reader.GetValue(3)?.ToString().Split('/');
                            var dia = fechaParts[0];
                            var mes = fechaParts[1];
                            var anio = fechaParts[2].Substring(0, 4);

                            if (int.TryParse(dia, out int diaInt) && diaInt < 10)
                            {
                                // Si el día es menor a 10, el formato es MM/dd/yyyy
                                //mes = fechaParts[0];
                                //dia = fechaParts[1];
                            }

                            try
                            {
                                lBoleta.Fecha = new DateTime(Convert.ToInt32(anio), Convert.ToInt32(mes), Convert.ToInt32(dia));

                            }
                            catch (Exception ex)
                            {
                                // Manejar el error de fecha (puedes optar por lanzar una excepción personalizada si lo deseas)
                                throw new FormatException($"La fecha '{dia}/{mes}/{anio}' no es válida.");
                            }

                            boletas.Add(lBoleta);

                            // Reportar progreso cada 1000 filas procesadas para no saturar SignalR
                            if (boletas.Count - lastProgressReported >= progressInterval)
                            {
                                lastProgressReported = boletas.Count;
                                // Progreso estimado basado en filas procesadas
                                var progress = Math.Min(95, (boletas.Count * 95) / 30000); // Máximo 95% durante lectura
                                _hubContext.Clients.All.SendAsync("UpdateProgress", progress).Wait();
                            }
                        }
                    }
                }
            }

            // Si hay celdas con formato incorrecto, lanzar excepción
            if (celdasProblematicas.Any())
            {
                throw new ExcelFormatException(
                    "El archivo Excel tiene celdas con formato incorrecto",
                    celdasProblematicas
                );
            }

            return boletas;
        }

        public Dictionary<string, int> CantidadBoletasDiaSemana(List<Boleta> Listado)
        {


            var lDiccionario = new Dictionary<string, int>();

            foreach (var boleta in Listado)
            {
                var fecha = boleta.Fecha.ToShortDateString();
                var diaSemana = boleta.Fecha.ToString("dddd", new CultureInfo("es-ES"));
                if (lDiccionario.ContainsKey(diaSemana))
                {
                    lDiccionario[diaSemana]++;
                }
                else
                {
                    lDiccionario[diaSemana] = 1;
                }
            }

            var diasOrdenados = new List<string> { "domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado" };
            var lDiccionarioOrdenado = new Dictionary<string, int>();

            foreach (var dia in diasOrdenados)
            {
                if (lDiccionario.ContainsKey(dia))
                {
                    lDiccionarioOrdenado[dia] = lDiccionario[dia];
                }
            }

            return lDiccionarioOrdenado;
        }



        public Dictionary<string, int> CantidadBoletasDiaSemanales(List<Boleta> Listado, int lastMonth, int lastYear)
        {
            var lDiccionario = new Dictionary<string, int>();

            foreach (var boleta in Listado)
            {
                if (boleta.Fecha.Month == lastMonth && boleta.Fecha.Year == lastYear)
                {
                    var inicioSemana = boleta.Fecha.AddDays(-(int)boleta.Fecha.DayOfWeek);
                    var finSemana = inicioSemana.AddDays(6);
                    var clave = $"{inicioSemana:dd/MM/yyyy} al {finSemana:dd/MM/yyyy}";

                    if (lDiccionario.ContainsKey(clave))
                    {
                        lDiccionario[clave]++;
                    }
                    else
                    {
                        lDiccionario[clave] = 1;
                    }
                }
            }

            return lDiccionario;
        }

        public List<SaldosMensuales> GetSaldosMensuales(List<Boleta> Listado)
        {
            var lSaldosMensuales = Listado
                .GroupBy(x => new { x.PeriodoAnio, x.PeriodoMes })
                .OrderByDescending(g => g.Key.PeriodoAnio).ThenByDescending(g => g.Key.PeriodoMes)
                .Select(g => new SaldosMensuales
                {
                    Periodo = $"{g.Key.PeriodoAnio} - {g.Key.PeriodoMes:D2}",
                    ImporteFacturadoBruto = g.Sum(y => y.Facturado),
                    ImporteCobradoBruto = g.Sum(y => y.Cobrado),
                    ImporteDebitadoBruto = g.Sum(y => y.Debitado),
                    ImportePendiente = g.Sum(y => y.Facturado) - (g.Sum(y => y.Cobrado) + g.Sum(y => y.Debitado)),
                })
                .ToList();

            foreach (var saldo in lSaldosMensuales)
            {
                saldo.ImportePendiente = saldo.ImporteFacturadoBruto - (saldo.ImporteCobradoBruto + saldo.ImporteDebitadoBruto);

                saldo.PorcentajeCobrado = Math.Round((saldo.ImporteCobradoBruto + saldo.ImporteDebitadoBruto) / saldo.ImporteFacturadoBruto * 100, 2);

                if (saldo.PorcentajeCobrado > 100)
                {
                    saldo.PorcentajeCobrado = 100;
                }
            }

            return lSaldosMensuales;
        }

        public Dictionary<DateTime, int> GetBoletasMensualesPublico(List<Boleta> Listado)
        {
            var boletasPorPeriodo = Listado
                .GroupBy(x => new { x.PeriodoAnio, x.PeriodoMes })
                .OrderByDescending(g => g.Key.PeriodoAnio).ThenByDescending(g => g.Key.PeriodoMes)
                .ToDictionary(
                    g => new DateTime(g.Key.PeriodoAnio, g.Key.PeriodoMes, 1),
                    g => g.Count()
                );

            return boletasPorPeriodo;
        }

        public double GetBoletaMayorValorUltimos3Meses(IEnumerable<Boleta> boletas)
        {
            var averageIncome = boletas.OrderByDescending(b => b.Facturado).FirstOrDefault().Facturado;
            return Math.Round(averageIncome, 2);
        }

        public double GetBoletaMenorValorUltimos3Meses(IEnumerable<Boleta> boletas)
        {
            var averageIncome = boletas.OrderBy(b => b.Facturado).FirstOrDefault().Facturado;
            return Math.Round(averageIncome, 2);
        }

        public List<Entidad> GetEntidades(IEnumerable<Boleta> boletas)
        {
            var lReturn = boletas
                .Where(b => b.Entidad != null)
                .Select(b => b.Entidad)
                .ToList();

            return lReturn;
        }

        public List<MontosPorPeriodo> GetMontosPorPeriodo(IEnumerable<Boleta> boletas, int lastYear, int lastMonth)
        {
            int[] EntidadesMes1 = { 96, 313, 66 };
            int[] EntidadesMes3 = { 500, 968, 847, 909 };

            var cutoffDate = new DateTime(lastYear, lastMonth, 1).AddMonths(-5);

            var boletasSinPago = boletas
                .Where(b => b.Cobrado == 0 &&
                    !(b.Debitado == b.Facturado) &&
                    new DateTime(b.PeriodoAnio, b.PeriodoMes, 1) >= cutoffDate)
                .ToList();

            var boletasPorPeriodo = boletasSinPago
                .GroupBy(b => new { b.PeriodoAnio, b.PeriodoMes })
                .ToList();

            var montosPorPeriodos = new List<MontosPorPeriodo>();

            foreach (var periodoGrupo in boletasPorPeriodo)
            {
                var boletaslistado = periodoGrupo.ToList();

                #region 1Mes
                var boletas1Mes = boletaslistado.Where(b => EntidadesMes1.Contains(b.EntidadCodigo));

                if (boletas1Mes.Count() > 0)
                {
                    var montosPorPeriodo = montosPorPeriodos.FirstOrDefault(m => m.Periodo == $"{periodoGrupo.Key.PeriodoAnio} - {periodoGrupo.Key.PeriodoMes + 1:D2}");
                    if (montosPorPeriodo == null)
                    {
                        montosPorPeriodo = new MontosPorPeriodo();
                        
                        var nuevoMes = periodoGrupo.Key.PeriodoMes + 1;
                        var nuevoAnio = periodoGrupo.Key.PeriodoAnio;

                        if (nuevoMes > 12)
                        {
                            nuevoAnio += nuevoMes / 12;
                            nuevoMes = nuevoMes % 12;
                        }

                        montosPorPeriodo.Periodo = $"{nuevoAnio} - {nuevoMes:D2}";
                        
                        montosPorPeriodo.MontosPorEntidad = new Dictionary<string, double>();
                        montosPorPeriodos.Add(montosPorPeriodo);
                                                
                        
                    }

                    foreach (var boleta in periodoGrupo.Where(b => EntidadesMes1.Contains(b.EntidadCodigo)))
                    {
                        var clave = $"{boleta.Entidad.Codigo} - {boleta.Entidad.Nombre}";

                        if (montosPorPeriodo.MontosPorEntidad.ContainsKey(clave))
                        {
                            montosPorPeriodo.MontosPorEntidad[clave] += boleta.Facturado;
                        }
                        else
                        {
                            montosPorPeriodo.MontosPorEntidad[clave] = boleta.Facturado;
                        }
                    }
                }
                #endregion

                #region 2Meses
                var boletas2Meses = boletaslistado.Where(b => !EntidadesMes1.Contains(b.EntidadCodigo) && !EntidadesMes3.Contains(b.EntidadCodigo));

                if (boletas2Meses.Count() > 0)
                {
                    var montosPorPeriodo = montosPorPeriodos.FirstOrDefault(m => m.Periodo == $"{periodoGrupo.Key.PeriodoAnio} - {periodoGrupo.Key.PeriodoMes + 2:D2}");
                    if (montosPorPeriodo == null)
                    {
                        montosPorPeriodo = new MontosPorPeriodo();
                    
                        var nuevoMes = periodoGrupo.Key.PeriodoMes + 2;
                        var nuevoAnio = periodoGrupo.Key.PeriodoAnio;

                        if (nuevoMes > 12)
                        {
                            nuevoAnio += nuevoMes / 12;
                            nuevoMes = nuevoMes % 12;
                        }

                        montosPorPeriodo.Periodo = $"{nuevoAnio} - {nuevoMes:D2}";
                        
                        montosPorPeriodo.MontosPorEntidad = new Dictionary<string, double>();
                        montosPorPeriodos.Add(montosPorPeriodo);
                    }

                    foreach (var boleta in periodoGrupo.Where(b => !EntidadesMes1.Contains(b.EntidadCodigo) && !EntidadesMes3.Contains(b.EntidadCodigo)))
                    {
                        var clave = $"{boleta.Entidad.Codigo} - {boleta.Entidad.Nombre}";

                        if (montosPorPeriodo.MontosPorEntidad.ContainsKey(clave))
                        {
                            montosPorPeriodo.MontosPorEntidad[clave] += boleta.Facturado;
                        }
                        else
                        {
                            montosPorPeriodo.MontosPorEntidad[clave] = boleta.Facturado;
                        }
                    }
                }
                #endregion

                #region 3Meseses
                var boletas3Meses = boletaslistado.Where(b => EntidadesMes3.Contains(b.EntidadCodigo));

                if (boletas3Meses.Count() > 0)
                {
                    var montosPorPeriodo = montosPorPeriodos.FirstOrDefault(m => m.Periodo == $"{periodoGrupo.Key.PeriodoAnio} - {periodoGrupo.Key.PeriodoMes + 3:D2}");
                    if (montosPorPeriodo == null)
                    {
                        montosPorPeriodo = new MontosPorPeriodo();
                        var nuevoMes = periodoGrupo.Key.PeriodoMes + 3;
                        var nuevoAnio = periodoGrupo.Key.PeriodoAnio;

                        if (nuevoMes > 12)
                        {
                            
                            nuevoAnio += nuevoMes / 12;
                            nuevoMes = nuevoMes % 12;
                        }

                        montosPorPeriodo.Periodo = $"{nuevoAnio} - {nuevoMes:D2}";
                        
                        montosPorPeriodo.MontosPorEntidad = new Dictionary<string, double>();
                        montosPorPeriodos.Add(montosPorPeriodo);
                    }

                    foreach (var boleta in periodoGrupo.Where(b => EntidadesMes3.Contains(b.EntidadCodigo)))
                    {
                        var clave = $"{boleta.Entidad.Codigo} - {boleta.Entidad.Nombre}";

                        if (montosPorPeriodo.MontosPorEntidad.ContainsKey(clave))
                        {
                            montosPorPeriodo.MontosPorEntidad[clave] += boleta.Facturado;
                        }
                        else
                        {
                            montosPorPeriodo.MontosPorEntidad[clave] = boleta.Facturado;
                        }
                    }
                }
                #endregion
            }

            // Ordenar los MontosPorEntidad por el valor dentro de cada periodo
            var periodosUnicos = new Dictionary<string, MontosPorPeriodo>();

            foreach (var montosPorPeriodo in montosPorPeriodos)
            {
                if (periodosUnicos.ContainsKey(montosPorPeriodo.Periodo))
                {
                    var existente = periodosUnicos[montosPorPeriodo.Periodo];
                    foreach (var kv in montosPorPeriodo.MontosPorEntidad)
                    {
                        if (existente.MontosPorEntidad.ContainsKey(kv.Key))
                        {
                            existente.MontosPorEntidad[kv.Key] += kv.Value;
                        }
                        else
                        {
                            existente.MontosPorEntidad[kv.Key] = kv.Value;
                        }
                    }
                }
                else
                {
                    periodosUnicos[montosPorPeriodo.Periodo] = montosPorPeriodo;
                }
            }

            foreach (var montosPorPeriodo in periodosUnicos.Values)
            {
                montosPorPeriodo.MontosPorEntidad = montosPorPeriodo.MontosPorEntidad
                    .OrderByDescending(kv => kv.Value)
                    .ToDictionary(kv => kv.Key, kv => kv.Value);
            }

            montosPorPeriodos = periodosUnicos.Values.ToList();

            return montosPorPeriodos
                .OrderBy(m => int.Parse(m.Periodo.Split('-')[0].Trim()))
                .ThenBy(m => int.Parse(m.Periodo.Split('-')[1].Trim()))
                .ToList();
        }

        public Dictionary<string, int> GetEdadesPromedio(List<Boleta> Listado)
        {
            var gruposEtarios = new Dictionary<string, int>
            {
                { "0 a 15 años", 0 },
                { "16 a 30 años", 0 },
                { "31 a 50 años", 0 },
                { "51 a 65 años", 0 },
                { "66 a 75 años", 0 },
                { "76 a 90 años", 0 },
                { "mayores de 90 años", 0 }
            };

            foreach (var boleta in Listado)
            {
                if (boleta.Edad <= 15)
                {
                    gruposEtarios["0 a 15 años"]++;
                }
                else if (boleta.Edad <= 30)
                {
                    gruposEtarios["16 a 30 años"]++;
                }
                else if (boleta.Edad <= 50)
                {
                    gruposEtarios["31 a 50 años"]++;
                }
                else if (boleta.Edad <= 65)
                {
                    gruposEtarios["51 a 65 años"]++;
                }
                else if (boleta.Edad <= 75)
                {
                    gruposEtarios["66 a 75 años"]++;
                }
                else if (boleta.Edad <= 90)
                {
                    gruposEtarios["76 a 90 años"]++;
                }
                else
                {
                    gruposEtarios["mayores de 90 años"]++;
                }
            }

            return gruposEtarios;
        }

        public Dictionary<DateTime, double> GetFacturacionenUSD(List<SaldosMensuales> Listado, Dictionary<DateTime, double> Cotizaciones)
        {

            var facturacionEnUSD = new Dictionary<DateTime, double>();

            foreach (var saldo in Listado)
            {
                var periodo = DateTime.ParseExact(saldo.Periodo, "yyyy - MM", CultureInfo.InvariantCulture);
                if (Cotizaciones.TryGetValue(periodo, out var cotizacion))
                {
                    facturacionEnUSD[periodo] = Math.Round(saldo.ImporteFacturadoBruto / cotizacion, 2);
                }
            }

            return facturacionEnUSD;

        }

        public Dictionary<string, int> GetCantidadBoletasPorCirujano(List<Boleta> boletas)
        {
            var boletasPorCirujano = boletas
                .Where(b => b.Cirujano != null)
                .GroupBy(b => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(b.Cirujano!.ToLowerInvariant()))
                .OrderByDescending(g => g.Count())
                .ToDictionary(g => g.Key, g => g.Count());

            return boletasPorCirujano;
        }

        public Dictionary<string, int> GetCantidadBoletasPorHospital(List<Boleta> boletas)
        {
            var boletasPorHospital = boletas
                .Where(b => b.Hospital != null)
                .GroupBy(b => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(b.Hospital!.ToLowerInvariant()))
                .OrderByDescending(g => g.Count())
                .ToDictionary(g => g.Key, g => g.Count());

            return boletasPorHospital;
        }

        internal List<ItemCirujanoDTO> GetFacturacionPorCirujano(List<Boleta> boletas)
        {
            var totalFacturado = boletas.Sum(b => b.Facturado);

            var facturacionPorCirujano = boletas
                .GroupBy(b => string.IsNullOrEmpty(b.Cirujano) ? "Desconocido" : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(b.Cirujano.ToLowerInvariant()))
                .Select(g => new ItemCirujanoDTO
                {
                    Nombre = g.Key,
                    Cantidad = g.Count(),
                    Monto = Math.Round(g.Sum(b => b.Facturado), 2),
                    Porcentaje = Math.Round(g.Sum(b => b.Facturado) / totalFacturado * 100, 2)
                })
                .OrderByDescending(item => item.Monto)
                .ToList();

            return facturacionPorCirujano;
        }

        public List<string> GetPeriodosPrevios(List<Boleta> boletas, int ultimoAnio, int ultimoMes)
        {

            var ultimoPeriodoDate = new DateTime(ultimoAnio, ultimoMes, 1);
            var periodosPrevios = boletas
                .Select(b => new DateTime(b.PeriodoAnio, b.PeriodoMes, 1))
                .Where(d => d <= ultimoPeriodoDate)
                .Distinct()
                .OrderByDescending(d => d)
                .Select(d => d.ToString("yyyy - MM"))
                .ToList();

            return periodosPrevios;
        }


        public List<Boleta> GetBoletasPorPeriodo(List<Boleta> listado, string periodo)
        {
            var periodoDate = DateTime.ParseExact(periodo, "yyyy - MM", CultureInfo.InvariantCulture);
            return listado.Where(b => b.PeriodoAnio == periodoDate.Year && b.PeriodoMes == periodoDate.Month).ToList();
        }


         public List<Boleta> GetBoletasPorCirujano(List<Boleta> listado, string Cirujano)
        {
            return listado.Where(b => string.Equals(b.Cirujano?.ToUpperInvariant(), Cirujano.ToUpperInvariant(), StringComparison.OrdinalIgnoreCase)).ToList();
        }


         public List<Boleta> GetBoletasPorHospital(List<Boleta> listado, string Hospital)
        {
            return listado.Where(b => string.Equals(b.Hospital?.ToUpperInvariant(), Hospital.ToUpperInvariant(), StringComparison.OrdinalIgnoreCase)).ToList();
        }


        public Dictionary<double, double> GetDiferenciaMejorPeriodo(double mejorPeriodo, double importeUSD)
        {
            var diferencia = (importeUSD - mejorPeriodo) / mejorPeriodo * 100;
            var valorAbsolutoDiferencia = Math.Abs(importeUSD - mejorPeriodo);

            return new Dictionary<double, double> { { valorAbsolutoDiferencia, Math.Round(diferencia, 2) } };
        }

        public Dictionary<string,double> GetMejorPeriodo(Dictionary<DateTime,double> Ingresos)
        {
            var mejorPeriodo = Ingresos.OrderByDescending(i => i.Value).FirstOrDefault();
            return new Dictionary<string, double> { { mejorPeriodo.Key.ToString("yyyy-MM"), Math.Abs(mejorPeriodo.Value) } };
        }

        public List<Boleta> GetBoletasParciales(List<Boleta> boletas)
        {
            return boletas.Where(b => b.Cobrado > 0 && b.Cobrado < b.Facturado && (b.Debitado == 0 || b.Debitado == null)).ToList();
        }

        public List<Boleta> GetBoletasConDebitos(List<Boleta> boletas)
        {
            return boletas.Where(b => b.Debitado != 0 && b.Debitado != null).ToList();
        }

        /// <summary>
        /// Convierte un valor de celda Excel a double de forma segura.
        /// Maneja casos donde la celda está mal formateada como fecha.
        /// </summary>
        private static double SafeParseDouble(object cellValue, int rowNumber, string columnLetter, ref List<string> celdasProblematicas)
        {
            if (cellValue == null)
                return 0;

            // Si ya es un número, devolverlo directamente
            if (cellValue is double d)
                return d;
            if (cellValue is int i)
                return i;
            if (cellValue is decimal dec)
                return (double)dec;

            var stringValue = cellValue.ToString()?.Trim();
            if (string.IsNullOrEmpty(stringValue))
                return 0;

            // Intentar parsear como número con formato estándar
            stringValue = stringValue.Replace(",", ".");
            if (double.TryParse(stringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
                return result;

            // Si contiene "/" o "a. m." o "p. m.", probablemente sea una fecha mal formateada
            if (stringValue.Contains("/") || stringValue.Contains("a. m.") || stringValue.Contains("p. m."))
            {
                var cellReference = $"{columnLetter}{rowNumber}";
                celdasProblematicas.Add($"{cellReference} (tiene formato Fecha, debería ser Número)");
                return 0;
            }

            // Si nada funciona, registrar como problemática
            var cellRef = $"{columnLetter}{rowNumber}";
            celdasProblematicas.Add($"{cellRef} (valor '{stringValue}' no válido)");
            return 0;
        }

        /// <summary>
        /// Convierte un valor de celda Excel a int de forma segura.
        /// </summary>
        private static int SafeParseInt(object cellValue, int rowNumber, string columnLetter, ref List<string> celdasProblematicas)
        {
            if (cellValue == null)
                return 0;

            // Si ya es un número, devolverlo directamente
            if (cellValue is int i)
                return i;
            if (cellValue is double d)
                return (int)d;
            if (cellValue is decimal dec)
                return (int)dec;

            var stringValue = cellValue.ToString()?.Trim();
            if (string.IsNullOrEmpty(stringValue))
                return 0;

            // Intentar parsear como entero
            if (int.TryParse(stringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out int result))
                return result;

            // Si contiene punto decimal, intentar parsear como double y redondear
            if (double.TryParse(stringValue.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double dResult))
                return (int)dResult;

            // Registrar como problemática
            var cellRef = $"{columnLetter}{rowNumber}";
            celdasProblematicas.Add($"{cellRef} (valor '{stringValue}' no válido para edad)");
            return 0;
        }
    }
}
