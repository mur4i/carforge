using System.Text.RegularExpressions;
using System.Xml.Linq;
using CarForge.Core.Io;
using CarForge.Core.Models;
using CarForge.Core.Parsing;

namespace CarForge.Core.Analysis;

public sealed record ValidationIssue(string Severity, string Kind, string Detail);

/// <summary>
/// Aponta o que está quebrado num pack: modelo sem .yft, stream órfão,
/// modkit referenciado que não existe, e XML malformado.
/// </summary>
public sealed class PackValidator
{
    private static readonly string[] StreamSeparators = { "+", "_", "^" };
    private static readonly Regex KitNameRx = new(@"<kitName>\s*([^<\s]+)\s*</kitName>", RegexOptions.IgnoreCase);

    public IReadOnlyList<ValidationIssue> Validate(VehiclePack pack)
    {
        var issues = new List<ValidationIssue>();

        // 1) modelo declarado sem nenhum .yft de stream
        foreach (var model in pack.Models.Keys)
            if (!HasStream(pack, model))
                issues.Add(new ValidationIssue("aviso", "modelo sem .yft",
                    $"'{model}' é declarado mas não tem arquivo de modelo (só meta)."));

        // 2) stream órfão (.yft que nenhum modelo usa)
        foreach (var key in pack.StreamFiles.Keys)
            if (!IsOwned(pack, key))
                issues.Add(new ValidationIssue("aviso", "stream órfão",
                    $"'{key}' existe no stream mas nenhum modelo o referencia (peso morto)."));

        // 3) modkit referenciado que não existe em nenhum carcols
        var definedKits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var carcols in Directory.EnumerateFiles(pack.RootPath, "carcols.meta", SearchOption.AllDirectories))
            foreach (Match m in KitNameRx.Matches(MetaText.Read(carcols)))
                definedKits.Add(m.Groups[1].Value);

        foreach (var carvar in Directory.EnumerateFiles(pack.RootPath, "carvariations.meta", SearchOption.AllDirectories))
            foreach (var (model, kit) in MetaParsers.ParseModelKits(carvar))
                if (!definedKits.Contains(kit))
                    issues.Add(new ValidationIssue("erro", "modkit faltando",
                        $"'{model}' usa o modkit '{kit}', que não existe em nenhum carcols — tuning quebra."));

        // 4) XML malformado (FiveM exige XML válido pra ler data file)
        foreach (var meta in Directory.EnumerateFiles(pack.RootPath, "*.meta", SearchOption.AllDirectories))
        {
            try { XDocument.Parse(MetaText.Read(meta)); }
            catch (Exception ex)
            {
                issues.Add(new ValidationIssue("erro", "XML malformado",
                    $"{Path.GetRelativePath(pack.RootPath, meta)}: {ex.Message}"));
            }
        }

        return issues;
    }

    private static bool HasStream(VehiclePack pack, string model) =>
        pack.StreamFiles.Keys.Any(k =>
            k.Equals(model, StringComparison.OrdinalIgnoreCase) ||
            StreamSeparators.Any(s => k.StartsWith(model + s, StringComparison.OrdinalIgnoreCase)));

    private static bool IsOwned(VehiclePack pack, string streamKey)
    {
        if (pack.Models.ContainsKey(streamKey)) return true;
        foreach (var sep in StreamSeparators)
        {
            var i = streamKey.IndexOf(sep, StringComparison.Ordinal);
            if (i > 0 && pack.Models.ContainsKey(streamKey[..i])) return true;
        }
        return false;
    }
}
