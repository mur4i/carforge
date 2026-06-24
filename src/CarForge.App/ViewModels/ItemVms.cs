using System.Collections.ObjectModel;
using CarForge.Core.Models;

namespace CarForge.App.ViewModels;

/// <summary>Uma ocorrência de um modelo (um bloco &lt;Item&gt; num vehicles.meta).</summary>
public sealed class OccurrenceVm
{
    public required string MetaPath { get; init; }
    public int BlockIndex { get; init; }
    /// <summary>true quando o grupo tem 2+ blocos no MESMO arquivo.</summary>
    public bool SameFileGroup { get; init; }

    public string? Handling { get; init; }
    public string? Audio { get; init; }
    public string? Txd { get; init; }
    public string? VehicleClass { get; init; }
    public string? Yft { get; init; }
    /// <summary>Caminho absoluto do .yft (pro viewer 3D).</summary>
    public string? YftAbsolute { get; init; }
    public double SizeKB { get; init; }
    public bool Tuning { get; init; }

    /// <summary>Hash do bloco — duas ocorrências com mesmo hash são idênticas.</summary>
    public string ContentHash { get; init; } = "";
    /// <summary>Todos os campos do bloco, pro diff genérico.</summary>
    public IReadOnlyDictionary<string, string> Fields { get; init; } =
        new Dictionary<string, string>();

    private string Short => MetaPath.Length > 44 ? "…" + MetaPath[^44..] : MetaPath;
    public string Display => SameFileGroup ? $"{Short}  ·  bloco #{BlockIndex + 1}" : Short;

    public string PreviewLabel => Yft is null || SizeKB <= 0
        ? "sem modelo (só meta)"
        : $"{SizeKB:N0} KB · .yft";
}

/// <summary>Grupo de declarações com o mesmo spawn name (candidato a duplicado).</summary>
public sealed class DuplicateGroupVm
{
    public required string Model { get; init; }
    public required ObservableCollection<OccurrenceVm> Occurrences { get; init; }
    public bool Collides { get; init; }
    /// <summary>true quando todas as cópias estão no mesmo vehicles.meta.</summary>
    public bool SameFile { get; init; }

    public int Count => Occurrences.Count;

    /// <summary>Todas as cópias são byte-idênticas (mesmo hash de bloco)?</summary>
    public bool AllIdentical =>
        Occurrences.Select(o => o.ContentHash).Distinct().Count() <= 1;

    public string Verdict => AllIdentical ? "idênticas" : "revisar";

    public string Badge =>
        $"{Count}× · {Verdict}" +
        (SameFile ? " · mesmo arquivo" : "") +
        (Collides ? " · colide" : "");

    /// <summary>"ok" (seguro), "warn" (revisar), pra colorir o item na lista.</summary>
    public string Kind => AllIdentical ? "ok" : "warn";
}

/// <summary>Linha de diff entre A e B no comparador.</summary>
public sealed class DiffRowVm
{
    public required string Label { get; init; }
    public required string ValueA { get; init; }
    public required string ValueB { get; init; }
    public bool IsDifferent { get; init; }
}
