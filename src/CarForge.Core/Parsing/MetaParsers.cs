using System.Text.RegularExpressions;
using CarForge.Core.Io;

namespace CarForge.Core.Parsing;

/// <summary>
/// Parsers por regex tolerante. Cada padrão aqui foi validado contra o pack
/// real skips_veiculos (1695 modelos, 805 duplicados, 783 com tuning).
/// </summary>
public static partial class MetaParsers
{
    [GeneratedRegex(@"<modelName>\s*([^<\s]+)\s*</modelName>", RegexOptions.IgnoreCase)]
    private static partial Regex ModelRx();

    [GeneratedRegex(@"<handlingId>\s*([^<\s]+)\s*</handlingId>", RegexOptions.IgnoreCase)]
    private static partial Regex HandlingRx();

    [GeneratedRegex(@"<audioNameHash>\s*([^<\s]*)\s*</audioNameHash>", RegexOptions.IgnoreCase)]
    private static partial Regex AudioRx();

    [GeneratedRegex(@"<txdRelationship>.*?<parent>([^<]+)</parent>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TxdRx();

    [GeneratedRegex(@"<kits>(.*?)</kits>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex KitsBlockRx();

    [GeneratedRegex(@"<Item>\s*([^<\s]+)\s*</Item>", RegexOptions.IgnoreCase)]
    private static partial Regex KitItemRx();

    /// <summary>Resultado bruto de um Item de vehicles.meta.</summary>
    public readonly record struct VehicleRecord(string Model, string? HandlingId, string? AudioHash, string? Txd);

    /// <summary>
    /// Extrai cada veículo de um vehicles.meta. Fatiamos por &lt;/Item&gt; — cada
    /// InitData é um bloco. Robusto a Items aninhados porque só lemos o modelName
    /// de topo de cada fatia.
    /// </summary>
    public static IEnumerable<VehicleRecord> ParseVehicles(string metaPath)
    {
        var text = MetaText.Read(metaPath);
        foreach (var block in text.Split("</Item>"))
        {
            var m = ModelRx().Match(block);
            if (!m.Success) continue;
            var model = m.Groups[1].Value.Trim();
            if (model.Length == 0) continue;

            yield return new VehicleRecord(
                model.ToLowerInvariant(),
                HandlingRx().Match(block) is { Success: true } h ? h.Groups[1].Value.ToLowerInvariant() : null,
                AudioRx().Match(block) is { Success: true } a ? a.Groups[1].Value.ToLowerInvariant() : null,
                TxdRx().Match(block) is { Success: true } t ? t.Groups[1].Value.ToLowerInvariant() : null);
        }
    }

    /// <summary>
    /// Liga modelo -> kits de tuning a partir de carvariations.meta.
    /// Fatiamos por &lt;modelName&gt; (cada janela é um modelo) e procuramos o
    /// bloco &lt;kits&gt; dentro dela. Ignora 0_default_modkit.
    /// </summary>
    public static IEnumerable<(string Model, string Kit)> ParseModelKits(string carVariationsPath)
    {
        var text = MetaText.Read(carVariationsPath);
        // split mantendo o delimitador <modelName>...</modelName>
        var parts = Regex.Split(text, @"(<modelName>\s*[^<\s]+\s*</modelName>)", RegexOptions.IgnoreCase);
        for (int i = 1; i < parts.Length; i += 2)
        {
            var md = ModelRx().Match(parts[i]);
            if (!md.Success) continue;
            var model = md.Groups[1].Value.ToLowerInvariant();
            var window = i + 1 < parts.Length ? parts[i + 1] : string.Empty;

            var kits = KitsBlockRx().Match(window);
            if (!kits.Success) continue;
            foreach (Match kit in KitItemRx().Matches(kits.Groups[1].Value))
            {
                var name = kit.Groups[1].Value.ToLowerInvariant();
                if (name is "0_default_modkit" or "") continue;
                yield return (model, name);
            }
        }
    }
}
