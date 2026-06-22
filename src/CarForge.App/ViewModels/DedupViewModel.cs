using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using CarForge.App.Mvvm;
using CarForge.App.Views;
using CarForge.Core.Analysis;
using CarForge.Core.Editing;
using CarForge.Core.Models;

namespace CarForge.App.ViewModels;

public sealed class DedupViewModel : ObservableObject
{
    private readonly Action _rescan;
    private VehiclePack? _pack;

    public ObservableCollection<DuplicateGroupVm> Groups { get; } = new();
    public ObservableCollection<DiffRowVm> Diffs { get; } = new();
    public ICommand RenameCommand { get; }
    public ICommand RemoveDuplicatesCommand { get; }

    public DedupViewModel(Action rescan)
    {
        _rescan = rescan;
        RenameCommand = new RelayCommand(_ => Rename(), _ => SelectedGroup is not null && _pack is not null);
        RemoveDuplicatesCommand = new RelayCommand(_ => RemoveDuplicates(),
            _ => SelectedGroup is { Count: > 1 } && OccurrenceA is not null && _pack is not null);
    }

    public void Load(VehiclePack pack, AnalysisResult analysis)
    {
        _pack = pack;
        var collisionSet = analysis.FleetCollisions.Select(c => c.Model).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Groups.Clear();
        foreach (var g in analysis.InternalDuplicates)
        {
            var occ = new ObservableCollection<OccurrenceVm>(
                pack.ModelOccurrences[g.Model].Select(rel => BuildOcc(pack, g.Model, rel)));
            Groups.Add(new DuplicateGroupVm
            {
                Model = g.Model,
                Occurrences = occ,
                Collides = collisionSet.Contains(g.Model),
            });
        }
        SelectedGroup = Groups.FirstOrDefault();
    }

    private static OccurrenceVm BuildOcc(VehiclePack pack, string model, string relMeta)
    {
        var v = pack.Models.TryGetValue(model, out var m) ? m : null;
        double sizeKb = 0; string? yft = null; string? yftAbs = null;
        if (pack.StreamFiles.TryGetValue(model, out var files))
        {
            var best = files.Where(f => f.EndsWith(".yft", StringComparison.OrdinalIgnoreCase))
                            .OrderByDescending(f => new FileInfo(f).Length).FirstOrDefault();
            if (best is not null)
            {
                sizeKb = new FileInfo(best).Length / 1024.0;
                yft = Path.GetRelativePath(pack.RootPath, best);
                yftAbs = best;
            }
        }
        return new OccurrenceVm
        {
            MetaPath = relMeta,
            Handling = v?.HandlingId,
            Audio = v?.AudioHash,
            Yft = yft,
            YftAbsolute = yftAbs,
            SizeKB = sizeKb,
            Tuning = v?.HasTuning ?? false,
        };
    }

    private DuplicateGroupVm? _selectedGroup;
    public DuplicateGroupVm? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (!Set(ref _selectedGroup, value)) return;
            OccurrenceA = value?.Occurrences.ElementAtOrDefault(0);
            OccurrenceB = value?.Occurrences.ElementAtOrDefault(1) ?? value?.Occurrences.ElementAtOrDefault(0);
        }
    }

    private OccurrenceVm? _occA;
    public OccurrenceVm? OccurrenceA { get => _occA; set { if (Set(ref _occA, value)) RebuildDiff(); } }

    private OccurrenceVm? _occB;
    public OccurrenceVm? OccurrenceB { get => _occB; set { if (Set(ref _occB, value)) RebuildDiff(); } }

    private void RebuildDiff()
    {
        Diffs.Clear();
        if (OccurrenceA is null || OccurrenceB is null) return;
        void Row(string label, string? a, string? b) => Diffs.Add(new DiffRowVm
        {
            Label = label, ValueA = a ?? "—", ValueB = b ?? "—",
            IsDifferent = !string.Equals(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase),
        });

        Row("handling", OccurrenceA.Handling, OccurrenceB.Handling);
        Row("áudio", OccurrenceA.Audio, OccurrenceB.Audio);
        Row(".yft", Path.GetFileName(OccurrenceA.Yft), Path.GetFileName(OccurrenceB.Yft));
        Row("tamanho", OccurrenceA.SizeKB > 0 ? $"{OccurrenceA.SizeKB:N0} KB" : null,
                       OccurrenceB.SizeKB > 0 ? $"{OccurrenceB.SizeKB:N0} KB" : null);
        Row("tuning", OccurrenceA.Tuning ? "sim" : "não", OccurrenceB.Tuning ? "sim" : "não");
    }

    private void Rename()
    {
        if (_pack is null || SelectedGroup is null) return;
        var dlg = new RenameDialog(SelectedGroup.Model) { Owner = Application.Current.MainWindow };
        if (dlg.ShowDialog() != true) return;

        var renamer = new VehicleRenamer();
        var opts = new RenameOptions { RenameHandling = dlg.RenameHandling, RenameModKit = dlg.RenameModKit };
        var plan = renamer.Plan(_pack, SelectedGroup.Model, dlg.NewName, opts);

        if (!plan.IsSafe)
        {
            MessageBox.Show("Não dá pra renomear com segurança:\n\n• " + string.Join("\n• ", plan.Warnings),
                "CarForge", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Renomear '{plan.OldModel}' → '{plan.NewModel}' vai alterar {plan.Changes.Count} itens " +
            "(metas + arquivos de stream).\n\nFaça backup antes. Continuar?",
            "Confirmar rename", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        renamer.Apply(_pack, plan, opts);
        MessageBox.Show($"Renomeado: {plan.OldModel} → {plan.NewModel} ({plan.Changes.Count} alterações).", "CarForge");
        _rescan();
    }

    private void RemoveDuplicates()
    {
        if (_pack is null || SelectedGroup is null || OccurrenceA is null) return;

        var remover = new DuplicateRemover();
        var plan = remover.Plan(_pack, SelectedGroup.Model, OccurrenceA.MetaPath);

        if (!plan.IsSafe)
        {
            MessageBox.Show("Nada pra remover:\n\n• " + string.Join("\n• ", plan.Warnings),
                "CarForge", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Manter a ocorrência em:\n{OccurrenceA.MetaPath}\n\n" +
            $"e REMOVER '{plan.Model}' de {plan.Changes.Count} outro(s) arquivo(s) .meta?\n\n" +
            "Os arquivos de stream (.yft/.ytd) NÃO são apagados (são compartilhados).\n" +
            "Faça backup antes. Continuar?",
            "Remover duplicados", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        remover.Apply(_pack, plan);
        MessageBox.Show($"Removido '{plan.Model}' de {plan.Changes.Count} arquivo(s). Mantida 1 cópia.", "CarForge");
        _rescan();
    }
}
