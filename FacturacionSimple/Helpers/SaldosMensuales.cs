using System;
using FacturacionSimple.Models;
namespace FacturacionSimple.Helpers
{
	public class SaldosMensuales
	{
		public SaldosMensuales()
		{
		}

		public string Periodo { get; set; }
		public int Porcentaje { get; set; }
		public Double ImportePendiente { get; set; }
        public Double ImporteFacturadoBruto { get; set; }
        public Double ImporteDebitadoBruto { get; set; }
        public Double ImporteCobradoBruto { get; set; }

		public int PorcentajeCobrado { get; set; }

    }

	public class Entidades
    {

        public List<Entidad> GetEntidadesPublicas(List<Entidad> entidades)
        {
			return entidades
            .Where(e => e.Nombre.StartsWith("MUNIC") || e.Nombre.StartsWith("MIN.DE"))
			.GroupBy(e => e.Codigo)
			.Select(g => g.First())
            .ToList();               
		}

		public List<Entidad> GetEntidadesPrivadas(List<Entidad> entidades)
		{
			var entidadesPublicas = GetEntidadesPublicas(entidades).Select(e => e.Codigo).ToList();
			return entidades
			.Where(e => !entidadesPublicas.Contains(e.Codigo))
			.GroupBy(e => e.Codigo)
			.Select(g => g.First())
			.ToList();
		}
    }
}

