using CarForge.Core.Editing;
using CarForge.Core.Generation;
using CarForge.Core.Scanning;
using Xunit;

namespace CarForge.Core.Tests;

public class SplitAndHandlingTests
{
    [Fact]
    public void Split_gera_um_resource_por_unidade_com_manifest()
    {
        var root = Directory.CreateTempSubdirectory("cf_src").FullName;
        WriteMeta(root, "data/carA/vehicles.meta", "<CVehicleModelInfo__InitDataList><InitDatas><Item><modelName>cara</modelName></Item></InitDatas></CVehicleModelInfo__InitDataList>");
        WriteMeta(root, "data/carB/vehicles.meta", "<CVehicleModelInfo__InitDataList><InitDatas><Item><modelName>carb</modelName></Item></InitDatas></CVehicleModelInfo__InitDataList>");
        Directory.CreateDirectory(Path.Combine(root, "stream"));
        File.WriteAllBytes(Path.Combine(root, "stream", "cara.yft"), new byte[] { 1 });

        var pack = new PackScanner().Scan(root);
        var outRoot = Directory.CreateTempSubdirectory("cf_out").FullName;
        var results = new PackSplitter().Split(pack, outRoot);

        Assert.Equal(2, results.Count);
        var carA = Directory.GetDirectories(outRoot).First(d => d.Contains("cara", StringComparison.OrdinalIgnoreCase));
        Assert.True(File.Exists(Path.Combine(carA, "fxmanifest.lua")));
        Assert.True(File.Exists(Path.Combine(carA, "data", "vehicles.meta")));
        Assert.True(File.Exists(Path.Combine(carA, "stream", "cara.yft")));
    }

    [Fact]
    public void Handling_le_e_salva_campo_so_no_carro_certo()
    {
        var root = Directory.CreateTempSubdirectory("cf_h").FullName;
        var path = Path.Combine(root, "handling.meta");
        File.WriteAllText(path, """
            <CHandlingDataMgr><HandlingData>
              <Item type="CHandlingData"><handlingName>CARA</handlingName><fMass value="1000.0"/><fBrakeForce value="0.5"/></Item>
              <Item type="CHandlingData"><handlingName>CARB</handlingName><fMass value="2000.0"/></Item>
            </HandlingData></CHandlingDataMgr>
            """);

        var editor = new HandlingEditor();
        var entries = editor.Load(path);
        Assert.Equal(2, entries.Count);

        var cara = entries.First(e => e.Name == "CARA");
        Assert.Equal("1000.0", cara.Get("fMass"));

        editor.SetField(cara, "fMass", "1234.0");
        var txt = File.ReadAllText(path);
        Assert.Contains("<handlingName>CARA</handlingName>", txt);
        Assert.Contains("1234.0", txt);
        Assert.Contains("2000.0", txt); // CARB intacto
    }

    private static void WriteMeta(string root, string rel, string content)
    {
        var full = Path.Combine(root, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }
}
