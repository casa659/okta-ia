namespace OktaIA.Web.Services;

// Porta direta do spark(seed, up) do mockup — curva senoidal determinística (não é dado real,
// é só o mesmo efeito visual "linha de tendência" usado nos cards de KPI do design original).
public static class SparkPath
{
    public static string Generate(int seed, bool descendente = false)
    {
        var p = "";
        for (var i = 0; i <= 20; i++)
        {
            var n = Math.Sin((i + seed) * 1.7) * 4 + Math.Cos((i + seed) * 0.9) * 3;
            var v = Math.Max(2, Math.Min(20, 11 + n + (descendente ? -i * 0.28 : i * 0.22)));
            p += (i == 0 ? "M" : "L") + (i * 5) + " " + (22 - v).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        }

        return p;
    }
}
