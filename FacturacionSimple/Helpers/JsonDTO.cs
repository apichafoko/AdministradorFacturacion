using System;

namespace FacturacionSimple.Helpers;

public class JsonDTO
{
    public DateTime date { get; set; }

    public string source { get; set; }

    public double value_sell { get; set; }

    public double value_buy { get; set; }
}


