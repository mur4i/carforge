using System.IO;
using System.Windows.Input;
using CarForge.App.Mvvm;
using CarForge.Core.Analysis;
using CarForge.Core.Models;
using CarForge.Core.Scanning;
using Microsoft.Win32;

namespace CarForge.App.ViewModels;

/// <summary>VM raiz: escaneia o pack uma vez e alimenta os módulos (abas).</summary>
public sealed class AppViewModel : ObservableObject
{
    public DedupViewModel Dedup { get; }
    public SplitViewModel Split { get; }
    public HandlingViewModel Handling { get; }
    public ValidateViewModel Validate { get; }
    public ImportViewModel Import { get; }

    public ICommand BrowseCommand { get; }
    public ICommand BrowseFleetCommand { get; }
    public ICommand ScanCommand { get; }

    public AppViewModel()
    {
        Dedup = new DedupViewModel(Rescan);
        Split = new SplitViewModel();
        Handling = new HandlingViewModel();
        Validate = new ValidateViewModel();
        Import = new ImportViewModel();

        BrowseCommand = new RelayCommand(_ => PackPath = PickFolder("Pasta do pack de veículos") ?? PackPath);
        BrowseFleetCommand = new RelayCommand(_ => FleetPath = PickFolder("Pasta resources (frota existente)") ?? FleetPath);
        ScanCommand = new AsyncRelayCommand(_ => ScanAsync(), _ => Directory.Exists(PackPath));
    }

    private string _packPath = "";
    public string PackPath { get => _packPath; set => Set(ref _packPath, value); }

    private string _fleetPath = "";
    public string FleetPath { get => _fleetPath; set => Set(ref _fleetPath, value); }

    private string _status = "Aponte a pasta do pack e clique em Escanear.";
    public string Status { get => _status; set => Set(ref _status, value); }

    public int ModelCount { get => _m; set => Set(ref _m, value); }   private int _m;
    public int DupCount { get => _d; set => Set(ref _d, value); }     private int _d;
    public int CollisionCount { get => _c; set => Set(ref _c, value); } private int _c;
    public int TuningCount { get => _t; set => Set(ref _t, value); }  private int _t;

    private void Rescan() => ((AsyncRelayCommand)ScanCommand).Execute(null);

    private async Task ScanAsync()
    {
        Status = "Escaneando…";
        try
        {
            var path = PackPath;
            var fleet = Directory.Exists(FleetPath) ? FleetPath : null;
            var (pack, analysis) = await Task.Run(() =>
            {
                var p = new PackScanner().Scan(path);
                var a = new PackAnalyzer().Analyze(p, fleet);
                return (p, a);
            });

            ModelCount = pack.UniqueModelCount;
            DupCount = analysis.InternalDuplicates.Count;
            CollisionCount = analysis.FleetCollisions.Count;
            TuningCount = analysis.TuningCount;

            Dedup.Load(pack, analysis);
            Split.Load(pack);
            Handling.Load(pack);
            Validate.Load(pack);

            Status = $"OK: {ModelCount} modelos · {DupCount} duplicados · {CollisionCount} colisões · {TuningCount} com tuning.";
        }
        catch (Exception ex)
        {
            Status = "Erro: " + ex.Message;
        }
    }

    private static string? PickFolder(string title)
    {
        var dlg = new OpenFolderDialog { Title = title };
        return dlg.ShowDialog() == true ? dlg.FolderName : null;
    }
}
