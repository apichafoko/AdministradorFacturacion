
using System;
namespace FacturacionSimple.Models
{
	public class MontosPorPeriodo
{
    public string Periodo { get; set; } // Formato "YYYY-MM"
    public Dictionary<string, double> MontosPorEntidad { get; set; } // Clave: Entidad, Valor: Monto
}
}

