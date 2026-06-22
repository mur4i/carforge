using System.Collections.ObjectModel;
using System.Windows.Input;
using CarForge.App.Mvvm;
using CarForge.Core.Analysis;
using CarForge.Core.Models;

namespace CarForge.App.ViewModels;

public sealed class ValidateViewModel : ObservableObject
{
    private VehiclePack? _pack;

    public ObservableCollection<ValidationIssue> Issues { get; } = new();
    public ICommand RunCommand { get; }

    public ValidateViewModel()
    {
        RunCommand = new AsyncRelayCommand(_ => RunAsync(), _ => _pack is not null);
    }

    public void Load(VehiclePack pack)
    {
        _pack = pack;
        Issues.Clear();
        Status = "Pack carregado. Clique em Validar.";
    }

    private string _status = "Escaneie um pack primeiro.";
    public string Status { get => _status; set => Set(ref _status, value); }

    private async Task RunAsync()
    {
        if (_pack is null) return;
        Status = "Validando…";
        try
        {
            var pack = _pack;
            var result = await Task.Run(() => new PackValidator().Validate(pack));
            Issues.Clear();
            foreach (var i in result.OrderByDescending(x => x.Severity == "erro")) Issues.Add(i);
            var erros = result.Count(i => i.Severity == "erro");
            Status = $"{result.Count} problema(s): {erros} erro(s), {result.Count - erros} aviso(s).";
        }
        catch (Exception ex) { Status = "Erro: " + ex.Message; }
    }
}
