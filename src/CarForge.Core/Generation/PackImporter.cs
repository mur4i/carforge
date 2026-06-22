namespace CarForge.Core.Generation;

public sealed record ImportResult(int StreamFiles, int MetaFiles, string ResourcePath);

/// <summary>
/// Pega arquivos soltos de um carro (de qualquer pasta) e monta um resource
/// limpo: stream/ pros modelos, data/ pros .meta, e o fxmanifest.lua gerado.
/// Aceita bagunça (arquivos espalhados em subpastas) — varre tudo recursivo.
/// </summary>
public sealed class PackImporter
{
    private static readonly string[] StreamExts = { ".yft", ".ytd", ".ydr", ".ydd", ".ycd", ".awc" };
    private static readonly string[] MetaNames = { "vehicles.meta", "carcols.meta", "carvariations.meta", "handling.meta", "vehiclelayouts.meta", "contentunlocks.meta" };
    private readonly ManifestGenerator _manifest = new();

    public ImportResult Import(string sourceFolder, string destResourceFolder, IProgress<string>? progress = null)
    {
        if (!Directory.Exists(sourceFolder))
            throw new DirectoryNotFoundException(sourceFolder);

        var streamDir = Path.Combine(destResourceFolder, "stream");
        var dataDir = Path.Combine(destResourceFolder, "data");
        Directory.CreateDirectory(streamDir);
        Directory.CreateDirectory(dataDir);

        int streams = 0, metas = 0;
        foreach (var file in Directory.EnumerateFiles(sourceFolder, "*", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(file);
            var ext = Path.GetExtension(name).ToLowerInvariant();

            if (Array.IndexOf(StreamExts, ext) >= 0)
            {
                File.Copy(file, UniqueDest(streamDir, name), overwrite: false);
                streams++;
                progress?.Report($"stream: {name}");
            }
            else if (MetaNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                File.Copy(file, UniqueDest(dataDir, name), overwrite: false);
                metas++;
                progress?.Report($"meta: {name}");
            }
        }

        File.WriteAllText(Path.Combine(destResourceFolder, "fxmanifest.lua"), _manifest.Generate(destResourceFolder));
        return new ImportResult(streams, metas, destResourceFolder);
    }

    // evita sobrescrever quando há nomes repetidos vindos de subpastas diferentes
    private static string UniqueDest(string dir, string name)
    {
        var dest = Path.Combine(dir, name);
        if (!File.Exists(dest)) return dest;
        var stem = Path.GetFileNameWithoutExtension(name);
        var ext = Path.GetExtension(name);
        for (int i = 2; ; i++)
        {
            var cand = Path.Combine(dir, $"{stem}_{i}{ext}");
            if (!File.Exists(cand)) return cand;
        }
    }
}
