using CarForge.Core.Models;
using CarForge.Core.Parsing;

namespace CarForge.Core.Scanning;

/// <summary>Indexa uma pasta/resource: modelos, ocorrências, stream e tuning.</summary>
public sealed class PackScanner
{
    private static readonly string[] StreamExts = { ".yft", ".ytd", ".ydr" };

    public VehiclePack Scan(string root, IProgress<string>? progress = null)
    {
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"Pasta não encontrada: {root}");

        var pack = new VehiclePack { RootPath = root };
        var vehicleMetas = new List<string>();
        var carVariations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(file).ToLowerInvariant();
            switch (name)
            {
                case "vehicles.meta": vehicleMetas.Add(file); pack.VehiclesMetaCount++; break;
                case "carvariations.meta": carVariations.Add(file); pack.CarVariationsCount++; break;
                case "carcols.meta": pack.CarColsCount++; break;
                case "handling.meta": pack.HandlingCount++; break;
                default:
                    var ext = Path.GetExtension(name);
                    if (Array.IndexOf(StreamExts, ext) >= 0)
                    {
                        var key = Path.GetFileNameWithoutExtension(name);
                        if (!pack.StreamFiles.TryGetValue(key, out var list))
                            pack.StreamFiles[key] = list = new List<string>();
                        list.Add(file);
                    }
                    break;
            }
        }

        // modelos + ocorrências (cada bloco <Item> preservado individualmente)
        foreach (var meta in vehicleMetas)
        {
            var rel = Path.GetRelativePath(root, meta);
            foreach (var rec in MetaParsers.ParseOccurrences(meta))
            {
                if (!pack.ModelOccurrences.TryGetValue(rec.Model, out var occ))
                    pack.ModelOccurrences[rec.Model] = occ = new List<string>();
                occ.Add(rel);

                if (!pack.Occurrences.TryGetValue(rec.Model, out var list))
                    pack.Occurrences[rec.Model] = list = new List<VehicleOccurrence>();
                list.Add(new VehicleOccurrence
                {
                    Model = rec.Model,
                    MetaRelPath = rel,
                    BlockIndex = rec.BlockIndex,
                    Fields = rec.Fields,
                    ContentHash = rec.ContentHash,
                });

                if (!pack.Models.ContainsKey(rec.Model))
                {
                    pack.Models[rec.Model] = new Vehicle
                    {
                        Model = rec.Model,
                        MetaPath = meta,
                        HandlingId = rec.Fields.GetValueOrDefault("handlingid"),
                        AudioHash = rec.Fields.GetValueOrDefault("audionamehash"),
                        Txd = rec.Fields.GetValueOrDefault("parent"),
                    };
                }
            }
        }

        // tuning
        foreach (var cv in carVariations)
            foreach (var (model, kit) in MetaParsers.ParseModelKits(cv))
                if (pack.Models.TryGetValue(model, out var v))
                    v.ModKits.Add(kit);

        progress?.Report($"Scan ok: {pack.UniqueModelCount} modelos, {pack.YftCount} .yft");
        return pack;
    }
}
