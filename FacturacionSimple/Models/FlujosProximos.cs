using System;
namespace FacturacionSimple.Models
{
	public class FlujosBrutosProximos
    {
		public FlujosBrutosProximos()
		{
		}

		public int Anio { get; set; }
		public int Mes { get; set; }
		public decimal MontoBruto { get; set; }
        public decimal MontoNeto { get; set; }
    }
}

