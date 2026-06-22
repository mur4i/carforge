using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CarForge.App.Mvvm;
using CarForge.Core.Editing;
using CarForge.Core.Models;

namespace CarForge.App.ViewModels;

/// <summary>Campo editável de handling (nome + valor bindável).</summary>
public sealed class HandlingFieldVm : ObservableObject
{
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    private string _value = "";
    public string Value { get => _value; set => Set(ref _value, value); }
}

public sealed class HandlingViewModel : ObservableObject
{
    private readonly HandlingEditor _editor = new();

    public ObservableCollection<HandlingEntry> Entries { get; } = new();
    public ObservableCollection<HandlingFieldVm> Fields { get; } = new();
    public ICommand SaveCommand { get; }

    public HandlingViewModel()
    {
        SaveCommand = new RelayCommand(_ => Save(), _ => SelectedEntry is not null);
    }

    public void Load(VehiclePack pack)
    {
        Entries.Clear();
        foreach (var e in _editor.LoadPack(pack).OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
            Entries.Add(e);
        Status = $"{Entries.Count} entradas de handling carregadas.";
        SelectedEntry = Entries.FirstOrDefault();
    }

    private string _status = "Escaneie um pack primeiro.";
    public string Status { get => _status; set => Set(ref _status, value); }

    private HandlingEntry? _selected;
    public HandlingEntry? SelectedEntry
    {
        get => _selected;
        set { if (Set(ref _selected, value)) BuildFields(); }
    }

    private void BuildFields()
    {
        Fields.Clear();
        if (SelectedEntry is null) return;
        // campos comuns primeiro, depois o resto
        foreach (var f in HandlingEditor.CommonFields)
            if (SelectedEntry.Fields.ContainsKey(f))
                Fields.Add(new HandlingFieldVm { Name = f, Value = SelectedEntry.Fields[f], Description = HandlingFieldDocs.Describe(f) });
        foreach (var kv in SelectedEntry.Fields)
            if (Array.IndexOf(HandlingEditor.CommonFields, kv.Key) < 0)
                Fields.Add(new HandlingFieldVm { Name = kv.Key, Value = kv.Value, Description = HandlingFieldDocs.Describe(kv.Key) });
    }

    private void Save()
    {
        if (SelectedEntry is null) return;
        try
        {
            foreach (var f in Fields)
                if (!string.Equals(SelectedEntry.Get(f.Name), f.Value, StringComparison.Ordinal))
                    _editor.SetField(SelectedEntry, f.Name, f.Value);
            Status = $"Handling de '{SelectedEntry.Name}' salvo.";
            MessageBox.Show($"Handling de '{SelectedEntry.Name}' salvo.", "CarForge");
        }
        catch (Exception ex) { Status = "Erro: " + ex.Message; }
    }
}
