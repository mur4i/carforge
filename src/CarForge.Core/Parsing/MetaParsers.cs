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

    /// <summary>Campo escalar do tipo &lt;tag&gt;valor&lt;/tag&gt;.</summary>
    private static readonly Regex LeafPairRx =
        new(@"<([A-Za-z_][\w]*)>([^<]*)</\1>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Campo do tipo &lt;tag value="x" /&gt; (ou com atributo value).</summary>
    private static readonly Regex ValueAttrRx =
        new(@"<([A-Za-z_][\w]*)\s+value=""([^""]*)""\s*/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Uma declaração concreta (bloco) de um veículo num vehicles.meta.</summary>
    public readonly record struct VehicleOccurrenceRecord(
        string Model, int BlockIndex, IReadOnlyDictionary<string, string> Fields, string ContentHash);

    /// <summary>
    /// Extrai cada DECLARAÇÃO de veículo como um bloco &lt;Item&gt; completo (com
    /// varredura de profundidade, robusta a &lt;Item&gt; aninhado), com seus campos
    /// escalares e um hash do conteúdo. <see cref="VehicleOccurrenceRecord.BlockIndex"/>
    /// é o índice da ocorrência DESTE modelo dentro do arquivo (0, 1, …).
    /// </summary>
    public static IEnumerable<VehicleOccurrenceRecord> ParseOccurrences(string metaPath)
    {
        var text = MetaText.Read(metaPath);
        var perModel = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (model, block) in EnumerateCarBlocks(text))
        {
            int idx = perModel.TryGetValue(model, out var c) ? c : 0;
            perModel[model] = idx + 1;
            yield return new VehicleOccurrenceRecord(model, idx, ExtractFields(block), HashBlock(block));
        }
    }

    private static IEnumerable<(string Model, string Block)> EnumerateCarBlocks(string text)
    {
        foreach (Match m in ModelRx().Matches(text))
        {
            var model = m.Groups[1].Value.Trim().ToLowerInvariant();
            if (model.Length == 0) continue;
            // o <Item> do carro é o de abertura mais próximo ANTES do modelName
            int open = text.LastIndexOf("<Item", m.Index, StringComparison.OrdinalIgnoreCase);
            if (open < 0) continue;
            int end = FindItemClose(text, open);
            if (end < 0) continue;
            yield return (model, text.Substring(open, end - open));
        }
    }

    private static int FindItemClose(string text, int open)
    {
        int depth = 0, i = open;
        while (i < text.Length)
        {
            int lt = text.IndexOf('<', i);
            if (lt < 0) break;
            if (TokenAt(text, lt, "</Item"))
            {
                depth--;
                int gt = text.IndexOf('>', lt);
                if (gt < 0) break;
                if (depth == 0) return gt + 1;
                i = gt + 1;
            }
            else if (TokenAt(text, lt, "<Item"))
            {
                int gt = text.IndexOf('>', lt);
                if (gt < 0) break;
                if (!(gt > 0 && text[gt - 1] == '/')) depth++;
                i = gt + 1;
            }
            else i = lt + 1;
        }
        return -1;
    }

    private static bool TokenAt(string text, int pos, string token) =>
        pos + token.Length <= text.Length &&
        text.AsSpan(pos, token.Length).Equals(token, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, string> ExtractFields(string block)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in LeafPairRx.Matches(block))
        {
            var k = m.Groups[1].Value.ToLowerInvariant();
            if (k is "item" || d.ContainsKey(k)) continue;
            d[k] = m.Groups[2].Value.Trim();
        }
        foreach (Match m in ValueAttrRx.Matches(block))
        {
            var k = m.Groups[1].Value.ToLowerInvariant();
            if (k is "item" || d.ContainsKey(k)) continue;
            d[k] = m.Groups[2].Value.Trim();
        }
        return d;
    }

    /// <summary>FNV-1a 64-bit do bloco com espaços colapsados.</summary>
    private static string HashBlock(string block)
    {
        var norm = Regex.Replace(block, @"\s+", " ").Trim();
        ulong h = 1469598103934665603UL;
        foreach (char ch in norm) { h ^= ch; h *= 1099511628211UL; }
        return h.ToString("x16");
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
