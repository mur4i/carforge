using System.Collections.ObjectModel;
using CarForge.Core.Models;

namespace CarForge.App.ViewModels;

/// <summary>Uma ocorrência de um modelo (uma entrada num vehicles.meta).</summary>
public sealed class OccurrenceVm
{
    public required string MetaPath { get; init; }
    public string? Handling { get; init; }
    public string? Audio { get; init; }
    public string? Yft { get; init; }
    /// <summary>Caminho absoluto do .yft (pro viewer 3D).</summary>
    public string? YftAbsolute { get; init; }
    public double SizeKB { get; init; }
    public bool Tuning { get; init; }

    public string Display => MetaPath.Length > 48 ? "…" + MetaPath[^48..] : MetaPath;
    public string PreviewLabel => Yft is null || SizeKB <= 0
        ? "sem modelo (só meta)"
        : $"{SizeKB:N0} KB · .yft";
}

/// <summary>Grupo de modelos com nome igual (candidato a duplicado).</summary>
public sealed class DuplicateGroupVm
{
    public required string Model { get; init; }
    public required ObservableCollection<OccurrenceVm> Occurrences { get; init; }
    public bool Collides { get; init; }

    public int Count => Occurrences.Count;

    /// <summary>"cópias idênticas" (mesmo yft+handling) vs "revisar".</summary>
    public bool NeedsReview =>
        Occurrences.Select(o => o.Handling).Distinct().Count() > 1 ||
        Occurrences.Select(o => o.Yft).Distinct().Count() > 1;

    public string Verdict => NeedsReview ? "revisar" : "redundante";
    public string Badge => $"{Count}× · {Verdict}{(Collides ? " · colide" : "")}";
}

/// <summary>Linha de diff entre A e B no comparador.</summary>
public sealed class DiffRowVm
{
    public required string Label { get; init; }
    public required string ValueA { get; init; }
    public required string ValueB { get; init; }
    public bool IsDifferent { get; init; }
}
