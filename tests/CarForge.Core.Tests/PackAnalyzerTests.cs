using CarForge.Core.Analysis;
using CarForge.Core.Scanning;
using Xunit;

namespace CarForge.Core.Tests;

public class PackAnalyzerTests
{
    [Fact]
    public void Detecta_duplicado_quando_mesmo_modelo_em_dois_metas()
    {
        var root = Directory.CreateTempSubdirectory("cf_pack").FullName;
        WriteMeta(root, "a/vehicles.meta", Veh("350z"));
        WriteMeta(root, "b/vehicles.meta", Veh("350z")); // duplicado
        WriteMeta(root, "c/vehicles.meta", Veh("blista"));

        var pack = new PackScanner().Scan(root);
        var dups = new PackAnalyzer().FindInternalDuplicates(pack);

        Assert.Single(dups);
        Assert.Equal("350z", dups[0].Model);
        Assert.Equal(2, dups[0].Occurrences.Count);
    }

    [Fact]
    public void Detecta_colisao_com_frota_externa()
    {
        var pack = Directory.CreateTempSubdirectory("cf_new").FullName;
        WriteMeta(pack, "vehicles.meta", Veh("frontiercore"));

        var fleet = Directory.CreateTempSubdirectory("cf_fleet").FullName;
        WriteMeta(fleet, "old/vehicles.meta", Veh("frontiercore"));
        WriteMeta(fleet, "old/vehicles.meta2.meta", Veh("outrocarro"));

        var scanned = new PackScanner().Scan(pack);
        var coll = new PackAnalyzer().FindFleetCollisions(scanned, fleet);

        Assert.Contains(coll, c => c.Model == "frontiercore");
    }

    private static string Veh(string model) => $"""
        <CVehicleModelInfo__InitDataList><InitDatas>
        <Item><modelName>{model}</modelName></Item>
        </InitDatas></CVehicleModelInfo__InitDataList>
        """;

    private static void WriteMeta(string root, string rel, string content)
    {
        var full = Path.Combine(root, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }
}
