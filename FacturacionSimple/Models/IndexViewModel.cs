using System;
using System.Collections.Generic;
using FacturacionSimple.Helpers;
using FacturacionSimple.Models;

namespace FacturacionSimple
{
	public class IndexViewModel
	{
		public IndexViewModel()
		{
		}

        //General
        public List<Boleta> ListadoBoletas { get; set; }
		public int CantidadBoletasProceadas { get; set; }
        public int CantidadBoletasConPagos { get; set; }
		public Dictionary<string, int> CantidadBoletasDia { get; set; }

        public Dictionary<string, int> CantidadBoletasDiaLastPeriod { get; set; }
        public double PromedioBoletaUltimoMesGeneral { get; set; }

        public List<SaldosMensuales> SaldosHistoricosPublicos {get;set;}
        public List<SaldosMensuales> SaldosHistoricosPrivados {get;set;}
       
        public Dictionary<string,int> EdadPacientes { get; set; }

        public Dictionary<string,int> BoletasPorEntidadGeneral { get; set; }

        public Dictionary<string,int> BoletasPorEntidadLastPeriod { get; set; }

        public int LastPeriod {get;set;}
        public int LastYear {get;set;}
        
        //Publico
		public int CantidadBoletasPublicas { get; set; }
        
        public Dictionary<DateTime,int> CantidadBoletasMensualesPublico { get; set;}

        public double PromedioBoletaUltimoMesPublico { get; set; }
        
        public double BoletaMenorValorU3MPublico { get; set; }
        public double BoletaMayorValorU3MPublico { get; set; }

        public double IngresoPromedioUltimoBrutoPublico { get; set; }

        public Dictionary<DateTime, double> IngresosMensualesPublico { get; set; }

        public List<FlujosBrutosProximos> FlujosProximosPublico { get; set; }
        public List<CobrosProximos> CobrosProximosPublico { get; set; }
        public List<MontosPorPeriodo> MontosProximosACobrarPublico { get; set; }

        public Dictionary<string, int> CantidadBoletasPorMutualPublico { get; set; }
        //Privado

        public int CantidadBoletasPrivadas { get; set; }
        
        public double PromedioBoletaUltimoMesPrivados { get; set; }
        
        public double BoletaMenorValorU3MPrivados { get; set; }
        public double BoletaMayorValorU3MPrivados { get; set; }

        public double IngresoPromedioUltimos3BrutoPrivados { get; set; }

        public Dictionary<DateTime, double> IngresosMensualesPrivados { get; set; }

        public List<FlujosBrutosProximos> FlujosProximosPrivados { get; set; }
        public List<CobrosProximos> CobrosProximosPrivado { get; set; }

        public Dictionary<string, int> CantidadBoletasPorMutualPrivados { get; set; }

        public double CantidadBoletasPagosParciales { get; set; }
        public double MontoRestantePagosParciales { get; set; }
        public Dictionary<string,int> EntidadesPagosParciales { get; set; }
        public double DebitosPrivados3Meses { get; set;}
        public double CantidadBoletasDebitadas3Meses { get; set; }
        public Dictionary<string,int> MutualesDebitos3Meses { get; set; }

    }
}

