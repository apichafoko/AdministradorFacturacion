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

        public Dictionary<string, int> CantidadBoletasDiaPublico { get; set; }

        public Dictionary<string, int> CantidadBoletasDiaPrivado { get; set; }

        public Dictionary<string, int> CantidadBoletasSemanalesPrivado { get; set; }

        public double PromedioBoletaUltimoMesGeneral { get; set; }

        public List<SaldosMensuales> SaldosHistoricosPublicos {get;set;}
        public List<SaldosMensuales> SaldosHistoricosPrivados {get;set;}
       
        public Dictionary<string,int> EdadPacientes { get; set; }

        public Dictionary<string,int> BoletasPorEntidadGeneral { get; set; }

        public Dictionary<string,int> BoletasPorEntidadLastPeriod { get; set; }

        public int LastPeriod {get;set;}
        public int LastYear {get;set;}
        
        public Dictionary<DateTime, double> IngresosTotalesUSD { get; set; }

        public Dictionary<DateTime, double> CotizacionesUSD { get; set; }
        public double CotizacionUSDLast {get;set;}


        //Publico
		public int CantidadBoletasPublicas { get; set; }
        
        public Dictionary<DateTime,int> CantidadBoletasMensualesPublico { get; set;}

        public double PromedioBoletaUltimoMesPublico { get; set; }
        
        public double BoletaMenorValorU3MPublico { get; set; }
        public double BoletaMayorValorU3MPublico { get; set; }

        public double IngresoPromedioUltimoBrutoPublico { get; set; }

        public Dictionary<DateTime, double> IngresosMensualesPublico { get; set; }

        public List<MontosPorPeriodo> MontosProximosACobrarPublico { get; set; }

        public Dictionary<string, int> CantidadBoletasPorMutualPublico { get; set; }
        //Privado

        public int CantidadBoletasPrivadas { get; set; }
        
        public double PromedioBoletaUltimoMesPrivados { get; set; }
        
        public double BoletaMenorValorU3MPrivados { get; set; }
        public double BoletaMayorValorU3MPrivados { get; set; }

        public double IngresoPromedioUltimoPrivado { get; set; }

        public Dictionary<DateTime, double> IngresosMensualesPrivados { get; set; }

        public List<MontosPorPeriodo> MontosProximosACobrarPrivado { get; set; }

        public Dictionary<string, int> CantidadBoletasPorMutualPrivados { get; set; }

        public Dictionary<DateTime,int> CantidadBoletasMensualesPrivado { get; set;}

        

        public Dictionary<string, int> CantidadBoletasPorHospitalPrivados { get; set; }

        public List<ItemCirujanoDTO> FacturacionPorCirujanoPrivados { get; set; }

        public double CantidadBoletasPagosParciales { get; set; }
        public double MontoRestantePagosParciales { get; set; }
        public Dictionary<string,int> EntidadesPagosParciales { get; set; }
        public double DebitosPrivados3Meses { get; set;}
        public double CantidadBoletasDebitadas3Meses { get; set; }
        public Dictionary<string,int> MutualesDebitos3Meses { get; set; }


        public List<string> PeriodosDisponibles {get;set;}

        public string PeriodoSeleccionado {get;set;}

        public int SelectedMonth {get;set;}
        public int SelectedYear {get;set;}

        public SelectedPeriodViewModel SelectedPeriodVM {get;set;}

        public List<Boleta> ListadoBoletasPublicas {get;set;}
        public List<Boleta> ListadoBoletasPrivadas {get;set;}

        public int CantidadBoletasLastPeriod {get;set;}

        public double IngresosTotalesLastPeriod {get;set;}

        public double IngresosTotalesUSDLastPeriod {get;set;}        
        public List<Boleta> ListadoBoletasLastPeriod {get;set;}

        public double IngresosMensualesPublicoLastPeriod {get;set;}
        public double IngresosMensualesPrivadoLastPeriod {get;set;}

        

        public Dictionary<double,double> DiferenciaMejorPeriodoTotal {get;set;}

        public string MejorPeriodo {get;set;}
        public double ImporteMejorPeriodo {get;set;}

        public List<Boleta> ListadoBoletasParciales {get;set;}
        public List<Boleta> ListadoBoletasDebitadas {get;set;}


        public List<Boleta> ListadoBoletasDebitadasLastPeriod {get;set;}


        public String CirujanoSeleccionado {get;set;}
        public String HospitalSeleccionado {get;set;}
        public List<Boleta> ListadoBoletasCirujanoSeleccionado {get;set;}

        public List<Boleta> ListadoBoletasHospitalSeleccionado {get;set;}
        public List<ResumenPeriodoDTO> BoletasPorPeriodoCirujanoSeleccionado {get;set;}

        public List<ResumenPeriodoDTO> BoletasPorPeriodoHospitalSeleccionado {get;set;}

        public List<string> CirujanosDisponibles {get;set;}
        public List<string> HospitalesDisponibles {get;set;}
    }
}

