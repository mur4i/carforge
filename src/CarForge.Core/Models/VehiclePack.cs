namespace CarForge.Core.Models;

/// <summary>Resultado do scan de uma pasta/resource de veículos.</summary>
public sealed class VehiclePack
{
    public required string RootPath { get; init; }

    /// <summary>Modelos únicos (1ª ocorrência de cada model name).</summary>
    public Dictionary<string, Vehicle> Models { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Todas as ocorrências de cada model name (pra detectar duplicado).
    /// Chave = model (minúsculo), valor = lista de caminhos relativos de vehicles.meta.</summary>
    public Dictionary<string, List<string>> ModelOccurrences { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Arquivos de stream indexados por nome-base minúsculo (sem extensão).</summary>
    public Dictionary<string, List<string>> StreamFiles { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int VehiclesMetaCount { get; set; }
    public int CarVariationsCount { get; set; }
    public int CarColsCount { get; set; }
    public int HandlingCount { get; set; }

    public int UniqueModelCount => Models.Count;
    public int YftCount => StreamFiles.SelectMany(kv => kv.Value).Count(p => p.EndsWith(".yft", StringComparison.OrdinalIgnoreCase));
}
