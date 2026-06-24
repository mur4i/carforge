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
            var occs = pack.Occurrences.TryGetValue(g.Model, out var list)
                ? list
                : new List<VehicleOccurrence>();
            bool sameFile = occs.Count > 1 &&
                            occs.Select(o => o.MetaRelPath).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1;

            var occVms = new ObservableCollection<OccurrenceVm>(
                occs.Select(o => BuildOcc(pack, o, sameFile)));

            Groups.Add(new DuplicateGroupVm
            {
                Model = g.Model,
                Occurrences = occVms,
                Collides = collisionSet.Contains(g.Model),
                SameFile = sameFile,
            });
        }
        SelectedGroup = Groups.FirstOrDefault();
    }

    private static OccurrenceVm BuildOcc(VehiclePack pack, VehicleOccurrence o, bool sameFileGroup)
    {
        double sizeKb = 0; string? yft = null; string? yftAbs = null;
        if (pack.StreamFiles.TryGetValue(o.Model, out var files))
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
        bool tuning = pack.Models.TryGetValue(o.Model, out var m) && m.HasTuning;

        return new OccurrenceVm
        {
            MetaPath = o.MetaRelPath,
            BlockIndex = o.BlockIndex,
            SameFileGroup = sameFileGroup,
            Handling = o.Handling,
            Audio = o.Audio,
            Txd = o.Txd,
            VehicleClass = o.VehicleClass,
            Yft = yft,
            YftAbsolute = yftAbs,
            SizeKB = sizeKb,
            Tuning = tuning,
            ContentHash = o.ContentHash,
            Fields = o.Fields,
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

    // ---- veredito (banner) e recomendação ----
    private string _verdictKind = "none";
    public string VerdictKind { get => _verdictKind; private set => Set(ref _verdictKind, value); }

    private string _verdictTitle = "";
    public string VerdictTitle { get => _verdictTitle; private set => Set(ref _verdictTitle, value); }

    private string _verdictDetail = "";
    public string VerdictDetail { get => _verdictDetail; private set => Set(ref _verdictDetail, value); }

    private string _recommendation = "";
    public string Recommendation { get => _recommendation; private set => Set(ref _recommendation, value); }

    // rótulos dos botões dependem do contexto (mesmo arquivo vs vários)
    private string _keepLabel = "Manter A, remover duplicados";
    public string KeepLabel { get => _keepLabel; private set => Set(ref _keepLabel, value); }

    /// <summary>Quando marcado, a remoção é aplicada direto, sem os diálogos de
    /// confirmação/sucesso. Fica memorizado durante a sessão.</summary>
    private bool _skipConfirm;
    public bool SkipConfirm { get => _skipConfirm; set => Set(ref _skipConfirm, value); }

    private static bool Eq(string? a, string? b) =>
        string.Equals(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase);

    private static string? Name(string? path) => path is null ? null : Path.GetFileName(path);

    private void RebuildDiff()
    {
        Diffs.Clear();
        var a = OccurrenceA; var b = OccurrenceB;
        if (a is null || b is null)
        {
            SetVerdict("none", "Selecione duas cópias", "Escolha A e B acima para comparar.");
            return;
        }

        bool sameOccurrence = ReferenceEquals(a, b);

        void Row(string label, string? av, string? bv)
        {
            Diffs.Add(new DiffRowVm
            {
                Label = label,
                ValueA = string.IsNullOrEmpty(av) ? "—" : av,
                ValueB = string.IsNullOrEmpty(bv) ? "—" : bv,
                IsDifferent = !sameOccurrence && !Eq(av, bv),
            });
        }

        // linhas curadas (o que um admin precisa ver sempre)
        Row("local", a.SameFileGroup ? $"bloco #{a.BlockIndex + 1}" : a.MetaPath,
                     b.SameFileGroup ? $"bloco #{b.BlockIndex + 1}" : b.MetaPath);
        Row("handling", a.Handling, b.Handling);
        Row("áudio (som do motor)", a.Audio, b.Audio);
        Row("txd (textura)", a.Txd, b.Txd);
        Row("classe", a.VehicleClass, b.VehicleClass);
        Row("modelo 3D (.yft)", Name(a.Yft), Name(b.Yft));
        Row("tamanho do .yft", a.SizeKB > 0 ? $"{a.SizeKB:N0} KB" : null, b.SizeKB > 0 ? $"{b.SizeKB:N0} KB" : null);
        Row("tuning", a.Tuning ? "sim" : "não", b.Tuning ? "sim" : "não");

        // qualquer OUTRO campo que difira e ainda não esteja coberto acima
        var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "handlingid", "audionamehash", "parent", "vehicleclass", "modelname" };
        var keys = a.Fields.Keys.Union(b.Fields.Keys, StringComparer.OrdinalIgnoreCase)
                    .Where(k => !covered.Contains(k))
                    .OrderBy(k => k, StringComparer.OrdinalIgnoreCase);
        foreach (var k in keys)
        {
            var av = a.Fields.GetValueOrDefault(k);
            var bv = b.Fields.GetValueOrDefault(k);
            if (!sameOccurrence && !Eq(av, bv)) Row(k, av, bv);
        }

        // veredito
        var diffLabels = Diffs.Where(d => d.IsDifferent).Select(d => d.Label).ToList();
        bool sharesModel = Eq(Name(a.Yft), Name(b.Yft));

        if (sameOccurrence)
        {
            SetVerdict("none", "É a mesma cópia dos dois lados",
                "Selecione uma cópia diferente em B para ver o que muda.");
            Recommendation = "";
        }
        else if (diffLabels.Count == 0 && string.Equals(a.ContentHash, b.ContentHash, StringComparison.Ordinal))
        {
            SetVerdict("ok", "Cópias idênticas — pode remover com segurança",
                "As duas declarações são iguais em tudo. Manter as duas só desperdiça espaço e pode gerar warning no load.");
            Recommendation = "Recomendado: manter 1 e remover a(s) outra(s). " +
                "Só os arquivos .meta são editados — o modelo 3D (.yft/.ytd) NÃO é apagado.";
        }
        else
        {
            var what = string.Join(", ", diffLabels);
            bool onlyAudio = diffLabels.Count == 1 &&
                             diffLabels[0].StartsWith("áudio", StringComparison.OrdinalIgnoreCase);
            SetVerdict("warn", "São o mesmo carro, mas com diferenças",
                $"Diferem em: {what}." + (sharesModel ? " O modelo 3D é o mesmo nas duas." : ""));
            Recommendation = onlyAudio
                ? "É o mesmo carro com som de motor diferente. Em A, escolha o som que você quer manter e remova a outra cópia. O .yft/.ytd não é tocado."
                : "Confira as diferenças acima. Em A, deixe a versão que você quer manter e remova as outras. O .yft/.ytd não é tocado.";
        }

        // rótulo do botão de manter
        KeepLabel = a.SameFileGroup
            ? $"Manter bloco #{a.BlockIndex + 1}, remover o resto"
            : "Manter A, remover as outras cópias";
    }

    private void SetVerdict(string kind, string title, string detail)
    {
        VerdictKind = kind; VerdictTitle = title; VerdictDetail = detail;
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
        var plan = remover.PlanKeeping(_pack, SelectedGroup.Model, OccurrenceA.MetaPath, OccurrenceA.BlockIndex);

        if (!plan.IsSafe)
        {
            MessageBox.Show("Nada pra remover:\n\n• " + string.Join("\n• ", plan.Warnings),
                "CarForge", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!SkipConfirm)
        {
            var keepDesc = OccurrenceA.SameFileGroup
                ? $"o bloco #{OccurrenceA.BlockIndex + 1} de:\n{OccurrenceA.MetaPath}"
                : $"a cópia em:\n{OccurrenceA.MetaPath}";

            var confirm = MessageBox.Show(
                $"Manter {keepDesc}\n\n" +
                $"e REMOVER as outras declarações de '{plan.Model}' em {plan.Changes.Count} arquivo(s) .meta?\n\n" +
                "Os arquivos de stream (.yft/.ytd) NÃO são apagados (são compartilhados).\n" +
                "Faça backup antes. Continuar?",
                "Remover duplicados", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;
        }

        remover.Apply(_pack, plan);
        if (!SkipConfirm)
            MessageBox.Show($"Removido '{plan.Model}' de {plan.Changes.Count} arquivo(s). Mantida 1 cópia.", "CarForge");
        _rescan();
    }
}
