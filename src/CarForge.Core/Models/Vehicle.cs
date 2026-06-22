namespace CarForge.Core.Models;

/// <summary>Um modelo de veículo encontrado num vehicles.meta.</summary>
public sealed class Vehicle
{
    /// <summary>spawn code, ex: "350z". Sempre minúsculo pra comparação.</summary>
    public required string Model { get; init; }

    /// <summary>Caminho absoluto do vehicles.meta onde foi declarado.</summary>
    public required string MetaPath { get; init; }

    public string? HandlingId { get; init; }
    public string? AudioHash { get; init; }
    public string? Txd { get; init; }

    /// <summary>Kits de tuning ligados a este modelo (via carvariations.meta).</summary>
    public HashSet<string> ModKits { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool HasTuning => ModKits.Count > 0;

    public override string ToString() => $"{Model} ({(HasTuning ? "tuning" : "stock")})";
}
