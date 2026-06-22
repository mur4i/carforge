using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using CarForge.App.Mvvm;
using CarForge.Core.Generation;
using CarForge.Core.Models;
using Microsoft.Win32;

namespace CarForge.App.ViewModels;

public sealed class SplitViewModel : ObservableObject
{
    private VehiclePack? _pack;

    public ObservableCollection<SplitResult> Results { get; } = new();
    public ICommand BrowseOutCommand { get; }
    public ICommand SplitCommand { get; }

    public SplitViewModel()
    {
        BrowseOutCommand = new RelayCommand(_ =>
        {
            var dlg = new OpenFolderDialog { Title = "Pasta de saída do split" };
            if (dlg.ShowDialog() == true) OutputPath = dlg.FolderName;
        });
        SplitCommand = new AsyncRelayCommand(_ => SplitAsync(),
            _ => _pack is not null && !string.IsNullOrWhiteSpace(OutputPath));
    }

    public void Load(VehiclePack pack) { _pack = pack; Status = "Pack carregado. Escolha a saída e clique em Separar."; }

    private string _outputPath = "";
    public string OutputPath { get => _outputPath; set => Set(ref _outputPath, value); }

    private bool _copyStream = true;
    public bool CopyStream { get => _copyStream; set => Set(ref _copyStream, value); }

    private string _status = "Escaneie um pack primeiro.";
    public string Status { get => _status; set => Set(ref _status, value); }

    private async Task SplitAsync()
    {
        if (_pack is null) return;
        Status = "Separando…";
        try
        {
            var pack = _pack; var outp = OutputPath; var copy = CopyStream;
            var results = await Task.Run(() =>
                new PackSplitter().Split(pack, outp, copy));

            Results.Clear();
            foreach (var r in results) Results.Add(r);
            Status = $"OK: {results.Count} resources gerados em {outp}.";
        }
        catch (Exception ex) { Status = "Erro: " + ex.Message; }
    }
}
