using System;
using FacturacionSimple.Helpers;

namespace FacturacionSimple.Models;

public class SelectedPeriodViewModel
{
 //Publico
		public int CantidadBoletasPublicas { get; set; }
        
        public double PromedioBoletaPublico { get; set; }
        
        public double BoletaMenorValorPublico { get; set; }
        public double BoletaMayorValorPublico { get; set; }

        public double IngresoPromedioBrutoPublico { get; set; }

        public Dictionary<string, int> CantidadBoletasPorHospitalPublico { get; set; }

    
        public Dictionary<string, int> CantidadBoletasPorMutualPublico { get; set; }
        //Privado

        public int CantidadBoletasPrivadas { get; set; }
        
        public double PromedioBoletaPrivados { get; set; }
        
        public double BoletaMenorValorPrivados { get; set; }
        public double BoletaMayorValorPrivados { get; set; }

        public double IngresoPromedioPrivado { get; set; }


        public double IngresosTotalesUSD {get;set;}

        public double CotizacionUSD {get;set;}


        public Dictionary<string, int> CantidadBoletasPorMutualPrivados { get; set; }

        public Dictionary<string, int> CantidadBoletasPorHospitalPrivados { get; set; }

        public List<ItemCirujanoDTO> FacturacionPorCirujanoPrivados { get; set; }

        public Dictionary<string,int> BoletasPorEntidad { get; set; }


        public Dictionary<string, int> CantidadBoletasDiaPublico { get; set; }

        public Dictionary<string, int> CantidadBoletasDiaPrivado { get; set; }


        public Dictionary<string, int> CantidadBoletasDia { get; set; }


        public int BoletasTotales { get; set; }

        public double IngresosTotales {get;set;}



        

}
