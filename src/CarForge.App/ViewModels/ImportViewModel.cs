using System.IO;
using System.Windows.Input;
using CarForge.App.Mvvm;
using CarForge.Core.Generation;
using Microsoft.Win32;

namespace CarForge.App.ViewModels;

public sealed class ImportViewModel : ObservableObject
{
    public ICommand BrowseSourceCommand { get; }
    public ICommand BrowseDestCommand { get; }
    public ICommand ImportCommand { get; }

    public ImportViewModel()
    {
        BrowseSourceCommand = new RelayCommand(_ => SourcePath = Pick("Pasta com os arquivos do carro (.yft/.meta)") ?? SourcePath);
        BrowseDestCommand = new RelayCommand(_ => DestPath = Pick("Onde criar o resource") ?? DestPath);
        ImportCommand = new AsyncRelayCommand(_ => ImportAsync(),
            _ => Directory.Exists(SourcePath) && !string.IsNullOrWhiteSpace(DestPath));
    }

    private string _sourcePath = "";
    public string SourcePath { get => _sourcePath; set => Set(ref _sourcePath, value); }

    private string _destPath = "";
    public string DestPath { get => _destPath; set => Set(ref _destPath, value); }

    private string _resourceName = "meu_carro";
    public string ResourceName { get => _resourceName; set => Set(ref _resourceName, value); }

    private string _status = "Escolha a pasta de origem e o destino.";
    public string Status { get => _status; set => Set(ref _status, value); }

    private async Task ImportAsync()
    {
        Status = "Importando…";
        try
        {
            var src = SourcePath;
            var dest = Path.Combine(DestPath, Sanitize(ResourceName));
            var result = await Task.Run(() => new PackImporter().Import(src, dest));
            Status = $"OK: {result.StreamFiles} arquivos de stream, {result.MetaFiles} metas → {result.ResourcePath}";
        }
        catch (Exception ex) { Status = "Erro: " + ex.Message; }
    }

    private static string Sanitize(string s)
    {
        var name = new string(s.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
        return string.IsNullOrWhiteSpace(name) ? "meu_carro" : name;
    }

    private static string? Pick(string title)
    {
        var dlg = new OpenFolderDialog { Title = title };
        return dlg.ShowDialog() == true ? dlg.FolderName : null;
    }
}
