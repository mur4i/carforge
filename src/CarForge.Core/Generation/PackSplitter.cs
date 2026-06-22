using System.Text.RegularExpressions;
using CarForge.Core.Models;
using CarForge.Core.Parsing;

namespace CarForge.Core.Generation;

public sealed record SplitResult(string ResourceName, int ModelCount, int FileCount);

/// <summary>
/// Quebra um pack monolítico em vários resources — um por "unidade"
/// (cada pasta que contém um vehicles.meta). As 596 pastas de 1 carro viram
/// resources limpos; os sub-packs multi-modelo ficam isolados pra você tratar.
/// Copia os .meta da unidade + os arquivos de stream dos modelos declarados +
/// gera o fxmanifest.lua. Não corta metas cirurgicamente (mais seguro).
/// </summary>
public sealed class PackSplitter
{
    private static readonly string[] StreamSeparators = { "+", "_", "^" };
    private readonly ManifestGenerator _manifest = new();

    public IReadOnlyList<SplitResult> Split(
        VehiclePack pack, string outputRoot, bool copyStream = true, IProgress<string>? progress = null)
    {
        Directory.CreateDirectory(outputRoot);
        var results = new List<SplitResult>();

        var units = Directory.EnumerateFiles(pack.RootPath, "vehicles.meta", SearchOption.AllDirectories)
            .Select(Path.GetDirectoryName)
            .Where(d => d is not null)
            .Distinct()
            .Cast<string>();

        foreach (var unitDir in units)
        {
            var rel = Path.GetRelativePath(pack.RootPath, unitDir);
            var resourceName = Sanitize(rel);
            var outDir = Path.Combine(outputRoot, resourceName);
            var outData = Path.Combine(outDir, "data");
            Directory.CreateDirectory(outData);

            // 1) copia os .meta da unidade
            int fileCount = 0;
            foreach (var meta in Directory.EnumerateFiles(unitDir, "*.meta"))
            {
                File.Copy(meta, Path.Combine(outData, Path.GetFileName(meta)), overwrite: true);
                fileCount++;
            }

            // 2) modelos declarados nesta unidade
            var models = MetaParsers.ParseVehicles(Path.Combine(unitDir, "vehicles.meta"))
                .Select(v => v.Model).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            // 3) stream dos modelos
            if (copyStream)
            {
                var outStream = Path.Combine(outDir, "stream");
                foreach (var model in models)
                    foreach (var src in StreamFilesFor(pack, model))
                    {
                        Directory.CreateDirectory(outStream);
                        File.Copy(src, Path.Combine(outStream, Path.GetFileName(src)), overwrite: true);
                        fileCount++;
                    }
            }

            // 4) manifest
            File.WriteAllText(Path.Combine(outDir, "fxmanifest.lua"), _manifest.Generate(outDir));

            results.Add(new SplitResult(resourceName, models.Count, fileCount));
            progress?.Report($"{resourceName}: {models.Count} modelo(s), {fileCount} arquivo(s)");
        }

        return results;
    }

    private static IEnumerable<string> StreamFilesFor(VehiclePack pack, string model)
    {
        foreach (var (key, paths) in pack.StreamFiles)
            if (key.Equals(model, StringComparison.OrdinalIgnoreCase) ||
                StreamSeparators.Any(s => key.StartsWith(model + s, StringComparison.OrdinalIgnoreCase)))
                foreach (var p in paths) yield return p;
    }

    private static string Sanitize(string rel)
    {
        var name = rel.Replace('\\', '/').Replace("/", "_");
        name = Regex.Replace(name, @"[^a-zA-Z0-9_]", "_").Trim('_').ToLowerInvariant();
        return name.Length == 0 ? "resource" : name;
    }
}
