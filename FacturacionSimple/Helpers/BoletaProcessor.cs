using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using ExcelDataReader;
using FacturacionSimple.Models;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using Microsoft.AspNetCore.SignalR;

namespace FacturacionSimple.Helpers
{
    public class BoletaProcessor
    {
        private readonly IHubContext<ProgressHub> _hubContext;


        public BoletaProcessor(IHubContext<ProgressHub> hubContext)
        {
            _hubContext = hubContext;

        }

        public async Task<List<Boleta>> ProcesarArchivo(string filePath)
        {


            var boletas = LeerBoletasDesdeArchivo(filePath);

            int totalBoletas = boletas.Count;
            int processedBoletas = 0;


            try
            {
                foreach (var boleta in boletas)
                {
                    // Incrementar boletas procesadas
                    processedBoletas++;

                    // Calcular el porcentaje de progreso
                    var porcentaje = (processedBoletas * 100) / totalBoletas;

                    // Enviar actualización de progreso al cliente
                    await _hubContext.Clients.All.SendAsync("ReceiveProgress", porcentaje);

                }

                // Enviar notificación de que el proceso ha terminado
                await _hubContext.Clients.All.SendAsync("ReceiveProgress", 100);
                return boletas;
            }
            catch (Exception ex)
            {
                // En caso de error, revertir la transacción
                // Enviar notificación de que el proceso ha terminado
                await _hubContext.Clients.All.SendAsync("ReceiveProgress", 100);
                throw; // Volver a lanzar la excepción para manejarla si es necesario
            }

        }


        private List<Boleta> LeerBoletasDesdeArchivo(string filePath)
        {
            var boletas = new List<Boleta>();

            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read))
            {
                using var reader = ExcelReaderFactory.CreateReader(stream);
                {
                    // Leer el archivo fila por fila
                    while (reader.Read())
                    {
                        if (reader.Depth > 2) // Saltar la cabecera
                        {
                            var lBoleta = new Boleta();

                            lBoleta.NumeroBoleta = Convert.ToInt64(reader.GetValue(1));
                            lBoleta.EntidadTexto = reader.GetValue(2)?.ToString();
                            lBoleta.Periodo = reader.GetValue(4)?.ToString();
                            lBoleta.PeriodoMes = Convert.ToDateTime(lBoleta.Periodo).Month;
                            lBoleta.PeriodoAnio = Convert.ToDateTime(lBoleta.Periodo).Year;

                            lBoleta.Hospital = reader.GetValue(5)?.ToString();
                            lBoleta.Edad = Convert.ToInt32(reader.GetValue(8)?.ToString());
                            lBoleta.Facturado = reader.GetValue(10) != null ? Convert.ToDouble(reader.GetValue(10).ToString().Replace(",", "."), CultureInfo.InvariantCulture) : 0;
                            lBoleta.Cobrado = reader.GetValue(11) != null ? Convert.ToDouble(reader.GetValue(11).ToString().Replace(",", "."), CultureInfo.InvariantCulture) : 0;
                            lBoleta.Debitado = reader.GetValue(12) != null ? Convert.ToDouble(reader.GetValue(12).ToString().Replace(",", "."), CultureInfo.InvariantCulture) : 0;
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
                            if (DateTime.TryParse(reader.GetValue(3)?.ToString(), out fechaBoleta))
                            {
                                lBoleta.Fecha = fechaBoleta;
                                lBoleta.Fecha = DateTime.ParseExact(fechaBoleta.ToString("dd/MM/yyyy"), "dd/MM/yyyy", CultureInfo.InvariantCulture).ToUniversalTime();
                            }
                            else
                            {
                                // Manejar el error de fecha (puedes optar por lanzar una excepción personalizada si lo deseas)
                                throw new FormatException($"La fecha '{reader.GetValue(3)?.ToString()}' no es válida.");
                            }


                            boletas.Add(lBoleta);
                        }
                    }
                }
            }

            return boletas;
        }





        public Dictionary<string, int> CantidadBoletasDiaSemana(List<Boleta> Listado)
        {
            var lDiccionario = new Dictionary<string, int>();

            foreach (var boleta in Listado)
            {
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
                        montosPorPeriodo.Periodo = $"{periodoGrupo.Key.PeriodoAnio} - {periodoGrupo.Key.PeriodoMes + 1:D2}";
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
                        montosPorPeriodo.Periodo = $"{periodoGrupo.Key.PeriodoAnio} - {periodoGrupo.Key.PeriodoMes + 2:D2}";
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
                        montosPorPeriodo.Periodo = $"{periodoGrupo.Key.PeriodoAnio} - {periodoGrupo.Key.PeriodoMes + 3:D2}";
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
            foreach (var montosPorPeriodo in montosPorPeriodos)
            {
                montosPorPeriodo.MontosPorEntidad = montosPorPeriodo.MontosPorEntidad
                    .OrderByDescending(kv => kv.Value)
                    .ToDictionary(kv => kv.Key, kv => kv.Value);
            }

            return montosPorPeriodos;
        }
    
    
        public Dictionary<string,int> GetEdadesPromedio(List<Boleta> Listado)
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
    }
}

