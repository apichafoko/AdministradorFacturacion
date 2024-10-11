using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace FacturacionSimple.Models
{
    public partial class Boleta
    {
        public Boleta()
        {
            
        }

        public int Id { get; set; }
        public long NumeroBoleta { get; set; }
        public string? Cirujano { get; set; }
        public bool Gravado { get; set; }
        public string? Hospital { get; set; }
        public string? Afiliado { get; set; }
        public string? Paciente { get; set; }
        public DateTime Fecha { get; set; }
        public string Periodo { get; set; }

        public int PeriodoAnio { get; set; }

        public int PeriodoMes { get; set; }
        public int IdProfesional { get; set; }
        public double Facturado { get; set; }
        public double Cobrado { get; set; }
        public double Debitado { get; set; }
        public double Saldo { get; set; }
        public int IdEntidad { get; set; }
        public int Edad { get; set; }
        public double? MontoARecibir { get; set; }
        public long? BoletaRefacturada { get; set; }
        [NotMapped]
        public string EntidadTexto { get; set; }

        public int EntidadCodigo { get; set; }

        public Entidad Entidad { get; set; }

    }
}

