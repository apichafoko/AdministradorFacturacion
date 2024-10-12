using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace FacturacionSimple.Models
{
    public partial class Boleta
    {
        public Boleta()
        {
            
        }

        public long NumeroBoleta { get; set; }
        public string? Cirujano { get; set; }
        public string? Hospital { get; set; }
        public DateTime Fecha { get; set; }
        public string Periodo { get; set; }

        public int PeriodoAnio { get; set; }

        public int PeriodoMes { get; set; }
        public double Facturado { get; set; }
        public double Cobrado { get; set; }
        public double Debitado { get; set; }
        public double Saldo { get; set; }
        public int Edad { get; set; }
        [NotMapped]
        public string EntidadTexto { get; set; }

        public int EntidadCodigo { get; set; }

        public Entidad Entidad { get; set; }

    }
}

