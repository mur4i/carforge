using CarForge.Core.Models;
using CarForge.Core.Parsing;

namespace CarForge.Core.Analysis;

/// <summary>Conflito de nome de modelo: candidato a duplicado (decisão é do usuário!).</summary>
public sealed record DuplicateGroup(string Model, IReadOnlyList<string> Occurrences);

/// <summary>Modelo do pack que colide com a frota já instalada no server.</summary>
public sealed record Collision(string Model, string ExistingMetaPath);

public sealed record AnalysisResult(
    VehiclePack Pack,
    IReadOnlyList<DuplicateGroup> InternalDuplicates,
    IReadOnlyList<Collision> FleetCollisions,
    int TuningCount,
    int NoTuningCount);

/// <summary>Roda os motores de análise sobre um pack já escaneado.</summary>
public sealed class PackAnalyzer
{
    /// <summary>Nomes de modelo que aparecem em mais de um vehicles.meta.</summary>
    public IReadOnlyList<DuplicateGroup> FindInternalDuplicates(VehiclePack pack) =>
        pack.ModelOccurrences
            .Where(kv => kv.Value.Count > 1)
            .Select(kv => new DuplicateGroup(kv.Key, kv.Value))
            .OrderByDescending(g => g.Occurrences.Count)
            .ThenBy(g => g.Model)
            .ToList();

    /// <summary>
    /// Modelos do pack que JÁ existem numa frota externa (outros resources).
    /// Ignora o próprio pack se ele estiver dentro de <paramref name="existingRoot"/>.
    /// </summary>
    public IReadOnlyList<Collision> FindFleetCollisions(VehiclePack pack, string existingRoot)
    {
        var existing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var packFull = Path.GetFullPath(pack.RootPath);

        foreach (var meta in Directory.EnumerateFiles(existingRoot, "vehicles.meta", SearchOption.AllDirectories))
        {
            if (Path.GetFullPath(meta).StartsWith(packFull, StringComparison.OrdinalIgnoreCase))
                continue; // não conta o próprio pack
            foreach (var rec in MetaParsers.ParseVehicles(meta))
                existing.TryAdd(rec.Model, meta);
        }

        return pack.Models.Keys
            .Where(existing.ContainsKey)
            .Select(m => new Collision(m, existing[m]))
            .OrderBy(c => c.Model)
            .ToList();
    }

    public AnalysisResult Analyze(VehiclePack pack, string? existingRoot = null)
    {
        var dups = FindInternalDuplicates(pack);
        var coll = existingRoot is not null && Directory.Exists(existingRoot)
            ? FindFleetCollisions(pack, existingRoot)
            : Array.Empty<Collision>();
        var tuning = pack.Models.Values.Count(v => v.HasTuning);
        return new AnalysisResult(pack, dups, coll, tuning, pack.UniqueModelCount - tuning);
    }
}
