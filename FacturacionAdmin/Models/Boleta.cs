using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace FacturacionAdmin.Models
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
        public int IdProfesional { get; set; }
        public decimal Facturado { get; set; }
        public decimal Cobrado { get; set; }
        public decimal Debitado { get; set; }
        public decimal Saldo { get; set; }
        public int IdEntidad { get; set; }
        public int Edad { get; set; }
        public decimal? MontoARecibir { get; set; }
        public long? BoletaRefacturada { get; set; }
        [NotMapped]
        public string EntidadTexto { get; set; }


    }
}

