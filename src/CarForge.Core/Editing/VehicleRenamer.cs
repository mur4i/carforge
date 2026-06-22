using System.Text.RegularExpressions;
using CarForge.Core.Io;
using CarForge.Core.Models;

namespace CarForge.Core.Editing;

/// <summary>O que o rename vai mexer (uma linha do preview).</summary>
public sealed record RenameChange(string Kind, string Path, string Detail);

public sealed class RenameOptions
{
    /// <summary>Também renomeia handlingId/handlingName quando == modelo antigo.</summary>
    public bool RenameHandling { get; init; }
    /// <summary>Também renomeia o id do modkit (carcols + carvariations) que contém o nome antigo.</summary>
    public bool RenameModKit { get; init; }
}

public sealed class RenamePlan
{
    public required string OldModel { get; init; }
    public required string NewModel { get; init; }
    public List<RenameChange> Changes { get; } = new();
    public List<string> Warnings { get; } = new();
    public bool IsSafe => Warnings.Count == 0 && Changes.Count > 0;
}

/// <summary>
/// Renomeia o spawn name (modelName) de um veículo em TODOS os lugares,
/// pra não dar erro. Sempre gere um <see cref="RenamePlan"/> antes (preview) e
/// só então <see cref="Apply"/>. O matching é por token exato — "as350" não
/// pega "as350pc".
/// </summary>
public sealed class VehicleRenamer
{
    private static readonly string[] StreamSeparators = { "+", "_", "^" };

    public RenamePlan Plan(VehiclePack pack, string oldModel, string newModel, RenameOptions? options = null)
    {
        options ??= new RenameOptions();
        oldModel = oldModel.Trim().ToLowerInvariant();
        newModel = newModel.Trim().ToLowerInvariant();

        var plan = new RenamePlan { OldModel = oldModel, NewModel = newModel };

        if (!IsValidModelName(newModel))
            plan.Warnings.Add($"Nome inválido: '{newModel}'. Use só letras minúsculas, números e _ (máx 32).");
        if (oldModel == newModel)
            plan.Warnings.Add("Nome novo igual ao antigo.");
        if (pack.Models.ContainsKey(newModel))
            plan.Warnings.Add($"Já existe um modelo '{newModel}' no pack — isso criaria uma nova colisão.");
        if (!pack.Models.ContainsKey(oldModel))
            plan.Warnings.Add($"Modelo '{oldModel}' não encontrado no pack.");

        // 1) vehicles.meta + carvariations.meta -> <modelName>
        foreach (var meta in EnumMetas(pack.RootPath, "vehicles.meta")
                     .Concat(EnumMetas(pack.RootPath, "carvariations.meta")))
        {
            var text = MetaText.Read(meta);
            if (ModelTagRx(oldModel).IsMatch(text))
                plan.Changes.Add(new RenameChange("modelName",
                    Path.GetRelativePath(pack.RootPath, meta),
                    $"<modelName>{oldModel}</modelName> → {newModel}"));
        }

        // 2) arquivos de stream (nome.yft, nome+hi.yft, nome.ytd, ...)
        foreach (var file in StreamFilesFor(pack, oldModel))
        {
            var newName = ReplaceStreamBase(Path.GetFileName(file), oldModel, newModel);
            plan.Changes.Add(new RenameChange("stream",
                Path.GetRelativePath(pack.RootPath, file),
                $"{Path.GetFileName(file)} → {newName}"));
        }

        // 3) opcional: handling
        if (options.RenameHandling)
        {
            foreach (var meta in EnumMetas(pack.RootPath, "vehicles.meta"))
            {
                var text = MetaText.Read(meta);
                if (HandlingTagRx(oldModel).IsMatch(text))
                    plan.Changes.Add(new RenameChange("handlingId",
                        Path.GetRelativePath(pack.RootPath, meta), $"handlingId {oldModel} → {newModel}"));
            }
            foreach (var hmeta in EnumMetas(pack.RootPath, "handling.meta"))
            {
                var text = MetaText.Read(hmeta);
                if (HandlingNameRx(oldModel).IsMatch(text))
                    plan.Changes.Add(new RenameChange("handlingName",
                        Path.GetRelativePath(pack.RootPath, hmeta), $"handlingName {oldModel} → {newModel}"));
            }
        }

        // 4) opcional: modkit (id que contém o nome antigo)
        if (options.RenameModKit)
        {
            foreach (var meta in EnumMetas(pack.RootPath, "carcols.meta")
                         .Concat(EnumMetas(pack.RootPath, "carvariations.meta")))
            {
                var text = MetaText.Read(meta);
                if (text.Contains($"_{oldModel}_modkit", StringComparison.OrdinalIgnoreCase)
                    || text.Contains($"{oldModel}_modkit", StringComparison.OrdinalIgnoreCase))
                    plan.Changes.Add(new RenameChange("modkit",
                        Path.GetRelativePath(pack.RootPath, meta), $"modkit *{oldModel}* → *{newModel}*"));
            }
        }

        if (plan.Changes.Count == 0)
            plan.Warnings.Add("Nada pra renomear (nenhuma ocorrência encontrada).");

        return plan;
    }

    /// <summary>Executa o plano. Edita os .meta e move os arquivos de stream.</summary>
    public void Apply(VehiclePack pack, RenamePlan plan, RenameOptions? options = null)
    {
        options ??= new RenameOptions();
        var old = plan.OldModel; var neu = plan.NewModel;

        // textos
        foreach (var meta in EnumMetas(pack.RootPath, "vehicles.meta")
                     .Concat(EnumMetas(pack.RootPath, "carvariations.meta")))
            ReplaceInFile(meta, t => ModelTagRx(old).Replace(t, $"<modelName>{neu}</modelName>"));

        if (options.RenameHandling)
        {
            foreach (var meta in EnumMetas(pack.RootPath, "vehicles.meta"))
                ReplaceInFile(meta, t => HandlingTagRx(old).Replace(t, $"<handlingId>{neu}</handlingId>"));
            foreach (var hmeta in EnumMetas(pack.RootPath, "handling.meta"))
                ReplaceInFile(hmeta, t => HandlingNameRx(old).Replace(t, $"<handlingName>{neu}</handlingName>"));
        }

        if (options.RenameModKit)
            foreach (var meta in EnumMetas(pack.RootPath, "carcols.meta")
                         .Concat(EnumMetas(pack.RootPath, "carvariations.meta")))
                ReplaceInFile(meta, t => Regex.Replace(t, Regex.Escape($"{old}_modkit"), $"{neu}_modkit", RegexOptions.IgnoreCase));

        // arquivos de stream (depois dos textos)
        foreach (var file in StreamFilesFor(pack, old))
        {
            var dir = Path.GetDirectoryName(file)!;
            var target = Path.Combine(dir, ReplaceStreamBase(Path.GetFileName(file), old, neu));
            if (!File.Exists(target)) File.Move(file, target);
        }
    }

    // ---- helpers ----
    private static IEnumerable<string> EnumMetas(string root, string name) =>
        Directory.EnumerateFiles(root, name, SearchOption.AllDirectories);

    private static IEnumerable<string> StreamFilesFor(VehiclePack pack, string model)
    {
        foreach (var (key, paths) in pack.StreamFiles)
        {
            if (key.Equals(model, StringComparison.OrdinalIgnoreCase) ||
                StreamSeparators.Any(s => key.StartsWith(model + s, StringComparison.OrdinalIgnoreCase)))
                foreach (var p in paths) yield return p;
        }
    }

    private static string ReplaceStreamBase(string fileName, string oldModel, string newModel)
    {
        // troca só o prefixo do nome-base, preservando sufixo (+hi, _hi) e extensão
        var ext = Path.GetExtension(fileName);
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var suffix = baseName.Length > oldModel.Length ? baseName[oldModel.Length..] : string.Empty;
        return newModel + suffix + ext;
    }

    private static void ReplaceInFile(string path, Func<string, string> transform)
    {
        var text = MetaText.Read(path);
        var updated = transform(text);
        if (!ReferenceEquals(text, updated) && text != updated)
            File.WriteAllText(path, updated);
    }

    private static bool IsValidModelName(string name) =>
        name.Length is > 0 and <= 32 && Regex.IsMatch(name, "^[a-z0-9_]+$");

    private static Regex ModelTagRx(string model) =>
        new($@"<modelName>\s*{Regex.Escape(model)}\s*</modelName>", RegexOptions.IgnoreCase);
    private static Regex HandlingTagRx(string model) =>
        new($@"<handlingId>\s*{Regex.Escape(model)}\s*</handlingId>", RegexOptions.IgnoreCase);
    private static Regex HandlingNameRx(string model) =>
        new($@"<handlingName>\s*{Regex.Escape(model)}\s*</handlingName>", RegexOptions.IgnoreCase);
}
