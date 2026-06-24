using System.Collections.ObjectModel;
using System.IO;
using CarForge.App.Mvvm;
using CarForge.Core.Analysis;
using CarForge.Core.Models;

namespace CarForge.App.ViewModels;

/// <summary>Uma linha da lista "Veículos" — um modelo único escaneado.</summary>
public sealed class VehicleRowVm
{
    public required string Model { get; init; }
    public int OccurrenceCount { get; init; }
    public string? Handling { get; init; }
    public string? Audio { get; init; }
    public string? VehicleClass { get; init; }
    public bool Tuning { get; init; }
    public bool HasYft { get; init; }
    public double SizeKB { get; init; }
    public string? YftAbsolute { get; init; }
    public bool IsDuplicate { get; init; }
    public bool Collides { get; init; }

    public string OccText => $"{OccurrenceCount}×";
    public string HandlingText => string.IsNullOrEmpty(Handling) ? "—" : Handling!;
    public string AudioText => string.IsNullOrEmpty(Audio) ? "—" : Audio!;
    public string ClassText => string.IsNullOrEmpty(VehicleClass) ? "—" : VehicleClass!;
    public string TuningText => Tuning ? "sim" : "—";
    public string SizeText => HasYft ? $"{SizeKB:N0} KB" : "sem .yft";

    /// <summary>"warn" = duplicado/colide (precisa de atenção); "ok" = normal.</summary>
    public string Kind => Collides || IsDuplicate ? "warn" : "ok";

    public string Tags =>
        (IsDuplicate ? "duplicado " : "") + (Collides ? "colide " : "") + (Tuning ? "tuning" : "");

    private string Haystack =>
        $"{Model} {Handling} {Audio} {VehicleClass}".ToLowerInvariant();

    public bool Matches(string q) => q.Length == 0 || Haystack.Contains(q);
}

/// <summary>Lista todos os modelos escaneados, com busca e preview 3D.</summary>
public sealed class VehiclesViewModel : ObservableObject
{
    private static readonly StringComparer OIC = StringComparer.OrdinalIgnoreCase;
    private List<VehicleRowVm> _all = new();

    public ObservableCollection<VehicleRowVm> Rows { get; } = new();

    public void Load(VehiclePack pack, AnalysisResult analysis)
    {
        var dupSet = analysis.InternalDuplicates.Select(d => d.Model).ToHashSet(OIC);
        var collSet = analysis.FleetCollisions.Select(c => c.Model).ToHashSet(OIC);

        _all = pack.Occurrences.Keys.OrderBy(m => m, OIC).Select(model =>
        {
            var occs = pack.Occurrences[model];
            var first = occs[0];

            double sizeKb = 0; string? yftAbs = null; bool hasYft = false;
            if (pack.StreamFiles.TryGetValue(model, out var files))
            {
                var best = files.Where(f => f.EndsWith(".yft", StringComparison.OrdinalIgnoreCase))
                                .OrderByDescending(f => new FileInfo(f).Length).FirstOrDefault();
                if (best is not null) { hasYft = true; sizeKb = new FileInfo(best).Length / 1024.0; yftAbs = best; }
            }
            bool tuning = pack.Models.TryGetValue(model, out var v) && v.HasTuning;

            return new VehicleRowVm
            {
                Model = model,
                OccurrenceCount = occs.Count,
                Handling = first.Handling,
                Audio = first.Audio,
                VehicleClass = first.VehicleClass,
                Tuning = tuning,
                HasYft = hasYft,
                SizeKB = sizeKb,
                YftAbsolute = yftAbs,
                IsDuplicate = dupSet.Contains(model),
                Collides = collSet.Contains(model),
            };
        }).ToList();

        Filter = "";       // dispara ApplyFilter
        ApplyFilter();
        SelectedRow = Rows.FirstOrDefault();
    }

    private string _filter = "";
    public string Filter
    {
        get => _filter;
        set { if (Set(ref _filter, value)) ApplyFilter(); }
    }

    private VehicleRowVm? _selectedRow;
    public VehicleRowVm? SelectedRow { get => _selectedRow; set => Set(ref _selectedRow, value); }

    private int _total;
    public int Total { get => _total; set => Set(ref _total, value); }

    public string CountText => $"{Rows.Count} de {_all.Count} veículos";

    private void ApplyFilter()
    {
        var q = _filter.Trim().ToLowerInvariant();
        Rows.Clear();
        foreach (var r in _all)
            if (r.Matches(q)) Rows.Add(r);
        Total = _all.Count;
        OnPropertyChanged(nameof(CountText));
        if (SelectedRow is null || !Rows.Contains(SelectedRow))
            SelectedRow = Rows.FirstOrDefault();
    }
}
