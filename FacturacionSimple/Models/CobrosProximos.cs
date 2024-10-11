using System;
namespace FacturacionSimple.Models
{
	public class CobrosProximos
	{
		public CobrosProximos()
		{
		}

		public int Anio { get; set; }
        public int Mes { get; set; }
		public DateTime FechaProbable { get; set; }
		public double MontoBruto { get; set; }
        public double MontoNeto { get; set; }
        public Entidad Entidad { get; set; }
    }
}

