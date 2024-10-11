using System;
using System.Globalization;
using ExcelDataReader;
using FacturacionAdmin.Data;
using FacturacionAdmin.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FacturacionAdmin.Helpers
{
    public class BoletaProcessor
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IHubContext<ProgressHub> _hubContext;


        public BoletaProcessor(ApplicationDbContext dbContext, IHubContext<ProgressHub> hubContext)
        {
            _dbContext = dbContext;
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


        public async Task ProcesarArchivo(string filePath, int IdProfesional)
        {
            var boletas = LeerBoletasDesdeArchivo(filePath, IdProfesional);

            int totalBoletas = boletas.Count;
            int processedBoletas = 0;

            using (var transaction = await _dbContext.Database.BeginTransactionAsync())
            {
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


                        // Obtener o crear la entidad desde el campo del archivo
                        var entidad = await ObtenerOCrearEntidad(boleta.EntidadTexto);  // Aquí boleta.EntidadTexto sería el valor crudo

                        // Verificar si la boleta ya existe en la base de datos
                        var boletaExistente = await _dbContext.Boletas
                            .FirstOrDefaultAsync(b => b.NumeroBoleta == boleta.NumeroBoleta);

                        if (boletaExistente != null)
                        {
                            // Verificar si el registro es diferente
                            if (!SonIguales(boletaExistente, boleta))
                            {
                                // Asegúrate de no modificar el `Id`

                                boletaExistente.Cirujano = boleta.Cirujano;
                                boletaExistente.Gravado = boleta.Gravado;
                                boletaExistente.Hospital = boleta.Hospital;
                                boletaExistente.Afiliado = boleta.Afiliado;
                                boletaExistente.Paciente = boleta.Paciente;
                                boletaExistente.Fecha = boleta.Fecha;
                                boletaExistente.Periodo = boleta.Periodo;
                                boletaExistente.IdProfesional = boleta.IdProfesional;
                                boletaExistente.Facturado = boleta.Facturado;
                                boletaExistente.Cobrado = boleta.Cobrado;
                                boletaExistente.Debitado = boleta.Debitado;
                                boletaExistente.Edad = boleta.Edad;

                                // No modifiques `boletaExistente.Id`

                                _dbContext.Boletas.Update(boletaExistente);
                            }
                        }
                        else
                        {
                            // Insertar si no existe
                            boleta.IdEntidad = entidad.Id; // Relacionar con la entidad encontrada o creada
                            await _dbContext.Boletas.AddAsync(boleta);
                        }
                    }

                    // Guardar los cambios en la base de datos
                    await _dbContext.SaveChangesAsync();
                    // Confirmar la transacción
                    await transaction.CommitAsync();

                    // Enviar notificación de que el proceso ha terminado
                    await _hubContext.Clients.All.SendAsync("ReceiveProgress", 100);
                }
                catch (Exception ex)
                {
                    // En caso de error, revertir la transacción
                    // Enviar notificación de que el proceso ha terminado
                    await _hubContext.Clients.All.SendAsync("ReceiveProgress", 100);
                    await transaction.RollbackAsync();
                    throw; // Volver a lanzar la excepción para manejarla si es necesario
                }
            }
        }

        private List<Boleta> LeerBoletasDesdeArchivo(string filePath, int IdProfesional)
        {
            var boletas = new List<Boleta>();

            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read))
            {
                using (var reader = ExcelReaderFactory.CreateReader(stream))
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
                            lBoleta.Hospital = reader.GetValue(5)?.ToString();
                            lBoleta.Edad = Convert.ToInt32(reader.GetValue(8)?.ToString());
                            lBoleta.Facturado = reader.GetValue(10) != null ? Convert.ToDecimal(reader.GetValue(10)) : 0;
                            lBoleta.Cobrado = reader.GetValue(11) != null ? Convert.ToDecimal(reader.GetValue(11)) : 0;
                            lBoleta.Debitado = reader.GetValue(12) != null ? Convert.ToDecimal(reader.GetValue(12)) : 0;
                            lBoleta.Saldo = lBoleta.Facturado - (lBoleta.Cobrado + lBoleta.Debitado);
                            lBoleta.IdProfesional = IdProfesional;


                            // Intentar parsear la fecha de forma flexible
                            DateTime fechaBoleta;
                            if (DateTime.TryParse(reader.GetValue(3)?.ToString(), out fechaBoleta))
                            {
                                lBoleta.Fecha = fechaBoleta;
                                lBoleta.Fecha = lBoleta.Fecha.ToUniversalTime();
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


        private List<Boleta> LeerBoletasDesdeArchivo(string filePath)
        {
            var boletas = new List<Boleta>();

            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read))
            {
                using (var reader = ExcelReaderFactory.CreateReader(stream))
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
                            lBoleta.Hospital = reader.GetValue(5)?.ToString();
                            lBoleta.Edad = Convert.ToInt32(reader.GetValue(8)?.ToString());
                            lBoleta.Facturado = reader.GetValue(10) != null ? Convert.ToDecimal(reader.GetValue(10)) : 0;
                            lBoleta.Cobrado = reader.GetValue(11) != null ? Convert.ToDecimal(reader.GetValue(11)) : 0;
                            lBoleta.Debitado = reader.GetValue(12) != null ? Convert.ToDecimal(reader.GetValue(12)) : 0;
                            lBoleta.Saldo = lBoleta.Facturado - (lBoleta.Cobrado + lBoleta.Debitado);


                            // Intentar parsear la fecha de forma flexible
                            DateTime fechaBoleta;
                            if (DateTime.TryParse(reader.GetValue(3)?.ToString(), out fechaBoleta))
                            {
                                lBoleta.Fecha = fechaBoleta;
                                lBoleta.Fecha = lBoleta.Fecha.ToUniversalTime();
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


        private bool SonIguales(Boleta boletaExistente, Boleta nuevaBoleta)
        {
            return boletaExistente.NumeroBoleta == nuevaBoleta.NumeroBoleta &&
                   boletaExistente.IdEntidad == nuevaBoleta.IdEntidad &&
                   boletaExistente.Fecha == nuevaBoleta.Fecha &&
                   boletaExistente.Periodo == nuevaBoleta.Periodo &&
                   boletaExistente.Hospital == nuevaBoleta.Hospital &&
                   boletaExistente.Facturado == nuevaBoleta.Facturado &&
                   boletaExistente.Cobrado == nuevaBoleta.Cobrado &&
                   boletaExistente.Debitado == nuevaBoleta.Debitado;
        }


        // Este método es el que extrae el número de entidad de la cadena, elimina los ceros iniciales,
        // y luego recupera o crea la entidad en la base de datos.
        private async Task<Entidad> ObtenerOCrearEntidad(string entidadTexto)
        {
            // Separar la parte numérica y el nombre
            var partes = entidadTexto.Split('/');

            if (partes.Length < 2)
                throw new FormatException("El formato de la entidad no es válido");

            // Extraer la parte numérica y eliminar ceros a la izquierda
            var codigoEntidadTexto = partes[0].TrimStart('0');

            if (!int.TryParse(codigoEntidadTexto, out int codigoEntidad))
                throw new FormatException("El código de la entidad no es un número válido");

            // Extraer el nombre de la entidad después de la barra
            var nombreEntidad = partes[1].Split('(')[0].Trim();

            // Buscar la entidad en la base de datos por código
            var entidad = await _dbContext.Entidades.FirstOrDefaultAsync(e => e.Codigo == codigoEntidad);

            if (entidad == null)
            {
                // Si no existe, crear una nueva entidad
                entidad = new Entidad
                {
                    Codigo = codigoEntidad,
                    Nombre = nombreEntidad
                };

                // Agregar la nueva entidad a la base de datos
                _dbContext.Entidades.Add(entidad);
                await _dbContext.SaveChangesAsync();
            }

            return entidad;
        }

    }
}

