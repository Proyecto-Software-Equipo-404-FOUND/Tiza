using System.Globalization;
using System.Text;

namespace PEI.IA.BenchmarkRunner;

public class BenchmarkItemResult
{
    public string PreguntaId { get; set; } = string.Empty;
    public string EnunciadoCorto { get; set; } = string.Empty;
    public string RespuestaId { get; set; } = string.Empty;
    public decimal NotaEsperada { get; set; }
    public decimal NotaIA { get; set; }
    public decimal Diferencia => Math.Abs(NotaIA - NotaEsperada);
    public int TokensIn { get; set; }
    public int TokensOut { get; set; }
    public long LatenciaMs { get; set; }
    public decimal CostoUsd { get; set; }
    public string Proveedor { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public string Justificacion { get; set; } = string.Empty;
}

public class BenchmarkSummary
{
    public int TotalEvaluaciones { get; set; }
    public decimal Mae { get; set; }
    public double PorcentajeDentroDe1Punto { get; set; }
    public double PorcentajeDentroDe2Puntos { get; set; }
    public double LatenciaPromedioMs { get; set; }
    public decimal CostoTotalUsd { get; set; }
    public int TotalTokensIn { get; set; }
    public int TotalTokensOut { get; set; }

    public static BenchmarkSummary Calcular(IReadOnlyList<BenchmarkItemResult> items)
    {
        if (items.Count == 0) return new BenchmarkSummary();

        var total = items.Count;
        var mae = items.Average(i => i.Diferencia);
        var dentro1 = items.Count(i => i.Diferencia <= 1.0m) / (double)total * 100.0;
        var dentro2 = items.Count(i => i.Diferencia <= 2.0m) / (double)total * 100.0;
        var latencia = items.Average(i => i.LatenciaMs);
        var costo = items.Sum(i => i.CostoUsd);
        var tokensIn = items.Sum(i => i.TokensIn);
        var tokensOut = items.Sum(i => i.TokensOut);

        return new BenchmarkSummary
        {
            TotalEvaluaciones = total,
            Mae = Math.Round(mae, 2),
            PorcentajeDentroDe1Punto = Math.Round(dentro1, 1),
            PorcentajeDentroDe2Puntos = Math.Round(dentro2, 1),
            LatenciaPromedioMs = Math.Round(latencia, 1),
            CostoTotalUsd = Math.Round(costo, 6),
            TotalTokensIn = tokensIn,
            TotalTokensOut = tokensOut
        };
    }

    public static void GuardarCsv(string rutaCsv, IEnumerable<BenchmarkItemResult> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("PreguntaId,RespuestaId,NotaEsperada,NotaIA,Diferencia,TokensIn,TokensOut,LatenciaMs,CostoUSD,Proveedor,Modelo,Justificacion");

        foreach (var item in items)
        {
            var justificacionEscapada = $"\"{item.Justificacion.Replace("\"", "\"\"").Replace("\n", " ").Replace("\r", "")}\"";
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0},{1},{2:0.##},{3:0.##},{4:0.##},{5},{6},{7},{8:0.000000},{9},{10},{11}",
                item.PreguntaId,
                item.RespuestaId,
                item.NotaEsperada,
                item.NotaIA,
                item.Diferencia,
                item.TokensIn,
                item.TokensOut,
                item.LatenciaMs,
                item.CostoUsd,
                item.Proveedor,
                item.Modelo,
                justificacionEscapada));
        }

        var dir = Path.GetDirectoryName(rutaCsv);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(rutaCsv, sb.ToString(), Encoding.UTF8);
    }
}
