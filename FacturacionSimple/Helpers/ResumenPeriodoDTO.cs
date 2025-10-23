using System;

namespace FacturacionSimple.Helpers;

public class ResumenPeriodoDTO
{
    public int PeriodoMes { get; set; }
    public int PeriodoAnio { get; set; }

    public string Hospital { get; set; }

    public string Cirujano { get; set; }

    public int Cantidad { get; set; }
}
