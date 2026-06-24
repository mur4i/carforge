namespace CarForge.Core.Models;

/// <summary>
/// Uma DECLARAÇÃO concreta de um veículo — um bloco &lt;Item&gt; dentro de um
/// vehicles.meta. Ao contrário de <see cref="Vehicle"/> (que guarda só a 1ª
/// ocorrência por nome), aqui cada bloco é preservado com seus campos, pra
/// comparar duas declarações do mesmo modelo e mostrar exatamente o que muda.
/// </summary>
public sealed class VehicleOccurrence
{
    public required string Model { get; init; }

    /// <summary>Caminho relativo (à raiz do pack) do vehicles.meta.</summary>
    public required string MetaRelPath { get; init; }

    /// <summary>Índice deste bloco entre as ocorrências DO MESMO modelo no
    /// arquivo (0, 1, 2…). Permite remover o bloco certo sem tocar nos outros.</summary>
    public required int BlockIndex { get; init; }

    /// <summary>Todos os campos escalares do bloco (tag minúscula → valor).</summary>
    public required IReadOnlyDictionary<string, string> Fields { get; init; }

    /// <summary>Hash do bloco normalizado — blocos com mesmo hash são idênticos.</summary>
    public required string ContentHash { get; init; }

    public string? Handling => Get("handlingid");
    public string? Audio => Get("audionamehash");
    public string? Txd => Get("parent");
    public string? GameName => Get("gamename");
    public string? Make => Get("vehiclemakename");
    public string? VehicleClass => Get("vehicleclass");

    private string? Get(string key) =>
        Fields.TryGetValue(key, out var v) && v.Length > 0 ? v : null;
}
