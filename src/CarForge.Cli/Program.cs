using CarForge.Core.Analysis;
using CarForge.Core.Generation;
using CarForge.Core.Scanning;

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
        """);
}
