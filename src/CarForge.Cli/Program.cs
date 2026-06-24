using BCnEncoder.Encoder;
using BCnEncoder.Shared;
using CarForge.Core.Analysis;
using CarForge.Core.Generation;
using CarForge.Core.Scanning;
using CodeWalker.GameFiles;
using CodeWalker.Utils;
using Microsoft.Toolkit.HighPerformance;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

// CarForge CLI — roda o motor sem precisar da GUI.
// Uso:
//   carforge analyze <pasta> [--against <pasta_resources>]
//   carforge manifest <pasta_resource> [--overwrite]

if (args.Length < 2)
{
    PrintUsage();
    return 1;
}

var command = args[0].ToLowerInvariant();
var target = args[1];

try
{
    switch (command)
    {
        case "analyze": return Analyze(target, GetOption(args, "--against"));
        case "manifest": return Manifest(target, HasFlag(args, "--overwrite"));
        case "split": return Split(target, GetOption(args, "--out"), !HasFlag(args, "--no-stream"));
        case "textures": return Textures(target);
        case "bake-livery": return BakeLivery(args);
        default:
            Console.Error.WriteLine($"Comando desconhecido: {command}");
            PrintUsage();
            return 1;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[erro] {ex.Message}");
    return 2;
}

static int Analyze(string path, string? against)
{
    Console.WriteLine($"[scan] {path}");
    var pack = new PackScanner().Scan(path, new Progress<string>(Console.WriteLine));
    var result = new PackAnalyzer().Analyze(pack, against);

    Console.WriteLine();
    Console.WriteLine("== RESUMO ==");
    Console.WriteLine($"  Modelos únicos : {pack.UniqueModelCount}");
    Console.WriteLine($"  vehicles.meta  : {pack.VehiclesMetaCount}");
    Console.WriteLine($"  .yft           : {pack.YftCount}");
    Console.WriteLine($"  Com tuning     : {result.TuningCount}");
    Console.WriteLine($"  Sem tuning     : {result.NoTuningCount}");

    Console.WriteLine();
    Console.WriteLine($"== DUPLICADOS INTERNOS: {result.InternalDuplicates.Count} ==");
    foreach (var d in result.InternalDuplicates.Take(15))
        Console.WriteLine($"  {d.Model}  ({d.Occurrences.Count}x)");
    if (result.InternalDuplicates.Count > 15)
        Console.WriteLine($"  ... +{result.InternalDuplicates.Count - 15}");

    if (against is not null)
    {
        Console.WriteLine();
        Console.WriteLine($"== COLISÃO COM FROTA: {result.FleetCollisions.Count} ==");
        foreach (var c in result.FleetCollisions.Take(30))
            Console.WriteLine($"  {c.Model}");
    }

    Console.WriteLine();
    Console.WriteLine("Dica: nome duplicado != carro duplicado. Confirme no viewer 3D antes de apagar.");
    return 0;
}

static int Manifest(string resourceRoot, bool overwrite)
{
    var gen = new ManifestGenerator();
    var content = gen.Generate(resourceRoot);
    gen.Write(resourceRoot, overwrite);
    Console.WriteLine($"[ok] fxmanifest.lua gerado em {resourceRoot}");
    Console.WriteLine(content);
    return 0;
}

static int Split(string packPath, string? outPath, bool copyStream)
{
    if (string.IsNullOrWhiteSpace(outPath))
    {
        Console.Error.WriteLine("Falta --out <pasta_saida>");
        return 1;
    }
    var pack = new PackScanner().Scan(packPath, new Progress<string>(Console.WriteLine));
    var results = new PackSplitter().Split(pack, outPath, copyStream, new Progress<string>(Console.WriteLine));
    Console.WriteLine($"[ok] {results.Count} resources gerados em {outPath}");
    return 0;
}

static int Textures(string path)
{
    if (!File.Exists(path)) { Console.Error.WriteLine($"Arquivo não encontrado: {path}"); return 1; }
    var bytes = File.ReadAllBytes(path);
    var ext = Path.GetExtension(path).ToLowerInvariant();

    var dicts = new List<(string Source, TextureDictionary Td)>();
    if (ext == ".ytd")
    {
        var ytd = new YtdFile();
        ytd.Load(bytes);
        if (ytd.TextureDict is not null) dicts.Add(("ytd", ytd.TextureDict));
    }
    else if (ext == ".yft")
    {
        var yft = new YftFile();
        yft.Load(bytes);
        var frag = yft.Fragment;
        var drawables = new List<DrawableBase>();
        if (frag?.Drawable is not null) drawables.Add(frag.Drawable);
        if (frag?.DrawableArray?.data_items is not null)
            foreach (var d in frag.DrawableArray.data_items) if (d is not null) drawables.Add(d);
        foreach (var d in drawables)
        {
            var td = d.ShaderGroup?.TextureDictionary;
            if (td is not null) dicts.Add(("yft-embebido", td));
        }
    }
    else { Console.Error.WriteLine("Use um arquivo .ytd ou .yft"); return 1; }

    Console.WriteLine($"== TEXTURAS de {Path.GetFileName(path)} ==");
    int n = 0;
    foreach (var (source, td) in dicts)
    {
        var items = td.Textures?.data_items;
        if (items is null) continue;
        foreach (var t in items)
        {
            if (t is null) continue;
            Console.WriteLine($"  {t.Name,-34} {t.Width,4}x{t.Height,-4} {t.Format}   [{source}]");
            n++;
        }
    }
    Console.WriteLine($"-- {n} textura(s) --");
    Console.WriteLine();
    Console.WriteLine("Pra plotagem (modo manual no mri_gtaeditor): txd = nome do modelo, txn = um destes nomes.");
    Console.WriteLine("Procure algo tipo *sign* / *livery* / *body*. Se só houver shader de tinta, a lataria não tem textura pra trocar.");
    return 0;
}

static int BakeLivery(string[] args)
{
    // bake-livery <carro.ytd> <design.png> <nomeTextura> [--out <saida.ytd>]
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Uso: bake-livery <carro.ytd> <design.png> <nomeTextura> [--out <saida.ytd>]");
        return 1;
    }
    var ytdPath = args[1];
    var pngPath = args[2];
    var texName = args[3].ToLowerInvariant();
    var outPath = GetOption(args, "--out")
        ?? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(ytdPath))!,
                        Path.GetFileNameWithoutExtension(ytdPath) + "_baked.ytd");

    if (!File.Exists(ytdPath)) { Console.Error.WriteLine($"YTD não encontrado: {ytdPath}"); return 1; }
    if (!File.Exists(pngPath)) { Console.Error.WriteLine($"PNG não encontrado: {pngPath}"); return 1; }

    // 1) PNG -> pixels (quadrado, potência de 2)
    using var image = Image.Load<Rgba32>(pngPath);
    int size = Math.Clamp(NearestPow2(Math.Max(image.Width, image.Height)), 64, 2048);
    if (image.Width != size || image.Height != size)
        image.Mutate(x => x.Resize(size, size));
    int w = image.Width, h = image.Height;
    var pixels = new ColorRgba32[h, w];
    image.ProcessPixelRows(accessor =>
    {
        for (int y = 0; y < h; y++)
        {
            var rowSpan = accessor.GetRowSpan(y);
            for (int x = 0; x < w; x++)
            {
                var p = rowSpan[x];
                pixels[y, x] = new ColorRgba32(p.R, p.G, p.B, p.A);
            }
        }
    });

    // 2) pixels -> DDS (BC3 / DXT5, com mips)
    var encoder = new BcEncoder();
    encoder.OutputOptions.GenerateMipMaps = true;
    encoder.OutputOptions.Quality = CompressionQuality.Balanced;
    encoder.OutputOptions.Format = CompressionFormat.Bc3;
    encoder.OutputOptions.FileFormat = OutputFileFormat.Dds;
    using var ms = new MemoryStream();
    encoder.EncodeToStream(pixels.AsMemory2D(), ms);
    var ddsBytes = ms.ToArray();

    // 3) DDS -> Texture do CodeWalker
    var newTex = DDSIO.GetTexture(ddsBytes);
    newTex.Name = texName;
    newTex.NameHash = JenkHash.GenHash(texName);

    // 4) carrega o YTD, troca/insere a textura, reconstrói e salva
    var ytd = new YtdFile();
    ytd.Load(File.ReadAllBytes(ytdPath));
    var dict = ytd.TextureDict;
    if (dict?.Textures?.data_items is null) { Console.Error.WriteLine("YTD sem dicionário de texturas."); return 1; }

    var list = dict.Textures.data_items.Where(t => t is not null).ToList();
    int idx = list.FindIndex(t => string.Equals(t.Name, texName, StringComparison.OrdinalIgnoreCase));
    string verb;
    if (idx >= 0) { list[idx] = newTex; verb = "substituída"; }
    else { list.Add(newTex); verb = "adicionada"; }
    dict.BuildFromTextureList(list);

    File.WriteAllBytes(outPath, ytd.Save());
    Console.WriteLine($"[ok] textura '{texName}' {verb} ({size}x{size}, BC3).");
    Console.WriteLine($"[ok] YTD salvo: {outPath}");
    Console.WriteLine("Ponha em stream/ do resource do carro (no lugar do .ytd original) e ensure no server.");
    return 0;
}

static int NearestPow2(int v) { int p = 1; while (p < v) p <<= 1; return p; }

static string? GetOption(string[] args, string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static bool HasFlag(string[] args, string name) => Array.IndexOf(args, name) >= 0;

static void PrintUsage()
{
    Console.WriteLine("""
        CarForge CLI

          carforge analyze <pasta> [--against <pasta_resources>]
              Escaneia um pack e lista duplicados, colisões e tuning.

          carforge manifest <pasta_resource> [--overwrite]
              Gera fxmanifest.lua pro resource.

          carforge split <pasta> --out <saida> [--no-stream]
              Quebra o monolito em 1 resource por unidade (com manifest).

          carforge textures <arquivo.ytd|.yft>
              Lista os nomes de textura do arquivo (via CodeWalker.Core).
              Use pra achar o txn certo de plotagem de um veículo.

          carforge bake-livery <carro.ytd> <design.png> <nomeTextura> [--out <saida.ytd>]
              Assa um PNG na textura <nomeTextura> do .ytd (BC3) e salva um .ytd
              novo — plotagem PERMANENTE. Ache o nome com 'textures'.
        """);
}
