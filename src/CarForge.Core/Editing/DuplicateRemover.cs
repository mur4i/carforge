using System.Text.RegularExpressions;
using CarForge.Core.Io;
using CarForge.Core.Models;

namespace CarForge.Core.Editing;

public sealed record RemovalChange(string MetaRelPath, string Kind);

public sealed class RemovalPlan
{
    public required string Model { get; init; }
    public required string KeepMetaRelPath { get; init; }
    public List<RemovalChange> Changes { get; } = new();
    public List<string> Warnings { get; } = new();
    public bool IsSafe => Changes.Count > 0;
}

/// <summary>
/// Remove as declarações DUPLICADAS de um modelo, mantendo só uma (a 'keeper').
/// Tira o bloco &lt;Item&gt; daquele modelo dos vehicles.meta e carvariations.meta
/// das outras ocorrências. NÃO mexe em stream (compartilhado) nem em carcols/
/// handling (ligados por id, não por modelo). Usa varredura de profundidade —
/// não quebra com &lt;Item&gt; aninhado dentro do carro.
/// </summary>
public sealed class DuplicateRemover
{
    public RemovalPlan Plan(VehiclePack pack, string model, string keepMetaRelPath)
    {
        model = model.Trim().ToLowerInvariant();
        var plan = new RemovalPlan { Model = model, KeepMetaRelPath = keepMetaRelPath };

        if (!pack.ModelOccurrences.TryGetValue(model, out var occ))
        {
            plan.Warnings.Add($"Modelo '{model}' não encontrado.");
            return plan;
        }
        var others = occ.Where(rel => !rel.Equals(keepMetaRelPath, StringComparison.OrdinalIgnoreCase))
                        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (others.Count == 0)
        {
            plan.Warnings.Add("Só há uma ocorrência — nada a remover.");
            return plan;
        }

        foreach (var rel in others)
        {
            var vm = Path.Combine(pack.RootPath, rel);
            if (File.Exists(vm) && CountItems(MetaText.Read(vm), model) > 0)
                plan.Changes.Add(new RemovalChange(rel, "vehicles.meta"));

            // carvariations no mesmo diretório
            var cv = Path.Combine(Path.GetDirectoryName(vm)!, "carvariations.meta");
            if (File.Exists(cv) && CountItems(MetaText.Read(cv), model) > 0)
                plan.Changes.Add(new RemovalChange(Path.GetRelativePath(pack.RootPath, cv), "carvariations.meta"));
        }

        if (plan.Changes.Count == 0)
            plan.Warnings.Add("Nada a remover (ocorrências não localizadas em disco).");
        return plan;
    }

    public void Apply(VehiclePack pack, RemovalPlan plan)
    {
        foreach (var ch in plan.Changes)
        {
            var path = Path.Combine(pack.RootPath, ch.MetaRelPath);
            if (!File.Exists(path)) continue;
            var text = MetaText.Read(path);
            var updated = RemoveItems(text, plan.Model);
            if (updated != text) File.WriteAllText(path, updated);
        }
    }

    // ---- núcleo: achar e remover o(s) <Item> que contêm <modelName>model</modelName> ----

    private static int CountItems(string text, string model)
    {
        int n = 0, from = 0;
        while (TryFindItemSpan(text, model, from, out var start, out var end))
        {
            n++; from = end;
        }
        return n;
    }

    private static string RemoveItems(string text, string model)
    {
        // remove de trás pra frente pra não bagunçar índices
        var spans = new List<(int start, int end)>();
        int from = 0;
        while (TryFindItemSpan(text, model, from, out var start, out var end))
        {
            spans.Add((start, end));
            from = end;
        }
        for (int i = spans.Count - 1; i >= 0; i--)
        {
            var (s, e) = spans[i];
            // engole espaços/linha em branco em volta
            int s2 = s; while (s2 > 0 && (text[s2 - 1] == ' ' || text[s2 - 1] == '\t')) s2--;
            int e2 = e; while (e2 < text.Length && (text[e2] == '\r' || text[e2] == '\n')) e2++;
            text = text.Remove(s2, e2 - s2);
        }
        return text;
    }

    private static readonly Regex ModelRx = new(@"<modelName>\s*([^<\s]+)\s*</modelName>", RegexOptions.IgnoreCase);

    /// <summary>
    /// Acha o span [start,end) do &lt;Item&gt; de topo (filho de InitDatas/variationData)
    /// que contém &lt;modelName&gt;model&lt;/modelName&gt;, a partir de 'searchFrom'.
    /// </summary>
    private static bool TryFindItemSpan(string text, string model, int searchFrom, out int start, out int end)
    {
        start = end = -1;
        var m = FindModelNameTag(text, model, searchFrom);
        if (m < 0) return false;

        // <Item> de abertura mais próximo ANTES do modelName (modelName vem no topo do carro,
        // antes de qualquer <Item> aninhado).
        int open = text.LastIndexOf("<Item", m, StringComparison.OrdinalIgnoreCase);
        if (open < 0) return false;

        // varredura de profundidade a partir de 'open'
        int depth = 0, i = open;
        while (i < text.Length)
        {
            int lt = text.IndexOf('<', i);
            if (lt < 0) break;
            if (Match(text, lt, "</Item"))
            {
                depth--;
                int gt = text.IndexOf('>', lt);
                if (gt < 0) break;
                if (depth == 0) { start = open; end = gt + 1; return true; }
                i = gt + 1;
            }
            else if (Match(text, lt, "<Item"))
            {
                int gt = text.IndexOf('>', lt);
                if (gt < 0) break;
                bool selfClosing = gt > 0 && text[gt - 1] == '/';
                if (!selfClosing) depth++;
                i = gt + 1;
            }
            else i = lt + 1;
        }
        return false;
    }

    private static int FindModelNameTag(string text, string model, int from)
    {
        foreach (Match mm in ModelRx.Matches(text, Math.Min(from, text.Length)))
            if (mm.Groups[1].Value.Equals(model, StringComparison.OrdinalIgnoreCase))
                return mm.Index;
        return -1;
    }

    private static bool Match(string text, int pos, string token) =>
        pos + token.Length <= text.Length &&
        text.AsSpan(pos, token.Length).Equals(token, StringComparison.OrdinalIgnoreCase);
}
