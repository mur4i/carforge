using CarForge.Core.Editing;
using CarForge.Core.Scanning;
using Xunit;

namespace CarForge.Core.Tests;

public class DuplicateRemoverTests
{
    [Fact]
    public void Remove_duplicado_mantendo_o_keeper()
    {
        var root = Directory.CreateTempSubdirectory("cf_del").FullName;
        WriteMeta(root, "keep/vehicles.meta", Veh("as350"));
        WriteMeta(root, "dup/vehicles.meta", Veh("as350"));

        var pack = new PackScanner().Scan(root);
        var remover = new DuplicateRemover();
        var plan = remover.Plan(pack, "as350", Path.Combine("keep", "vehicles.meta"));

        Assert.True(plan.IsSafe);
        remover.Apply(pack, plan);

        Assert.Contains("as350", File.ReadAllText(Path.Combine(root, "keep/vehicles.meta")));
        Assert.DoesNotContain("as350", File.ReadAllText(Path.Combine(root, "dup/vehicles.meta")));
    }

    [Fact]
    public void Remove_so_o_item_certo_em_meta_multi_modelo_com_item_aninhado()
    {
        var root = Directory.CreateTempSubdirectory("cf_del2").FullName;
        // keeper noutra pasta
        WriteMeta(root, "keep/vehicles.meta", Veh("as350"));
        // meta com 2 carros, e o as350 tem um <Item> ANINHADO (lista interna)
        var multi = """
            <CVehicleModelInfo__InitDataList><InitDatas>
              <Item>
                <modelName>as350</modelName>
                <drivableDoors><Item>VEH_EXT_DOOR_DSIDE_F</Item></drivableDoors>
              </Item>
              <Item>
                <modelName>outrocarro</modelName>
              </Item>
            </InitDatas></CVehicleModelInfo__InitDataList>
            """;
        WriteMeta(root, "multi/vehicles.meta", multi);

        var pack = new PackScanner().Scan(root);
        var plan = new DuplicateRemover().Plan(pack, "as350", Path.Combine("keep", "vehicles.meta"));
        new DuplicateRemover().Apply(pack, plan);

        var txt = File.ReadAllText(Path.Combine(root, "multi/vehicles.meta"));
        Assert.DoesNotContain("as350", txt);          // removido
        Assert.Contains("outrocarro", txt);           // intacto
        Assert.Contains("</InitDatas>", txt);         // estrutura preservada
    }

    private static string Veh(string model) =>
        $"<CVehicleModelInfo__InitDataList><InitDatas><Item><modelName>{model}</modelName></Item></InitDatas></CVehicleModelInfo__InitDataList>";

    private static void WriteMeta(string root, string rel, string content)
    {
        var full = Path.Combine(root, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }
}
