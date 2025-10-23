using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using ExcelDataReader;
using FacturacionSimple.Models;
using Microsoft.AspNetCore.SignalR;

namespace FacturacionSimple.Helpers;

public class CirujanoHelper
{
    public List<string> GetCirujanosDisponibles(List<Boleta> boletas)
        {
            var cirujanos = boletas
                .Where(b => b.Cirujano != null)
                .Select(b => b.Cirujano.ToUpperInvariant())
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            return cirujanos;
		}


    public List<ResumenPeriodoDTO> GetResumenPorPeriodo(List<Boleta> boletas)
    {
        var resumen = boletas
            .GroupBy(b => new { b.PeriodoAnio, b.PeriodoMes, b.Hospital, b.Cirujano })
            .Select(g => new ResumenPeriodoDTO
            {
                PeriodoAnio = g.Key.PeriodoAnio,
                PeriodoMes = g.Key.PeriodoMes,
                Cirujano = g.Key.Cirujano,
                Hospital = g.Key.Hospital,
                Cantidad = g.Count()
            })
            .OrderByDescending(r => r.PeriodoAnio)
            .ThenBy(r => r.PeriodoMes)
            .ThenBy(r => r.Cantidad)
            .ToList();

        return resumen;
    }
   

}
