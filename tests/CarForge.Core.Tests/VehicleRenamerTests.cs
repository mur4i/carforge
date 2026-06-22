using CarForge.Core.Editing;
using CarForge.Core.Scanning;
using Xunit;

namespace CarForge.Core.Tests;

public class VehicleRenamerTests
{
    [Fact]
    public void Plan_e_Apply_renomeiam_modelo_e_stream_em_todo_lugar()
    {
        var root = Directory.CreateTempSubdirectory("cf_ren").FullName;
        WriteVeh(root, "a/vehicles.meta", "as350pc", "as350pc");
        WriteVeh(root, "b/vehicles.meta", "as350pc", "as350pc");       // mesmo modelo em 2 metas
        File.WriteAllBytes(Path.Combine(MkDir(root, "stream"), "as350pc.yft"), new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(root, "stream", "as350pc+hi.yft"), new byte[] { 1 });

        var pack = new PackScanner().Scan(root);
        var renamer = new VehicleRenamer();
        var plan = renamer.Plan(pack, "as350pc", "as350pc_v2");

        Assert.True(plan.IsSafe, string.Join(";", plan.Warnings));
        Assert.Equal(2, plan.Changes.Count(c => c.Kind == "modelName")); // 2 metas
        Assert.Equal(2, plan.Changes.Count(c => c.Kind == "stream"));    // .yft + +hi.yft

        renamer.Apply(pack, plan);

        Assert.True(File.Exists(Path.Combine(root, "stream", "as350pc_v2.yft")));
        Assert.True(File.Exists(Path.Combine(root, "stream", "as350pc_v2+hi.yft")));
        Assert.Contains("as350pc_v2", File.ReadAllText(Path.Combine(root, "a/vehicles.meta")));
    }

    [Fact]
    public void Rename_nao_pega_modelo_por_substring()
    {
        var root = Directory.CreateTempSubdirectory("cf_sub").FullName;
        WriteVeh(root, "vehicles.meta", "as350", "as350pc"); // dois modelos no mesmo arquivo

        var pack = new PackScanner().Scan(root);
        var plan = new VehicleRenamer().Plan(pack, "as350", "as350x");

        new VehicleRenamer().Apply(pack, plan);
        var txt = File.ReadAllText(Path.Combine(root, "vehicles.meta"));

        Assert.Contains("<modelName>as350x</modelName>", txt);
        Assert.Contains("<modelName>as350pc</modelName>", txt); // intacto!
    }

    private static string MkDir(string root, string rel)
    {
        var d = Path.Combine(root, rel);
        Directory.CreateDirectory(d);
        return d;
    }

    private static void WriteVeh(string root, string rel, params string[] models)
    {
        var full = Path.Combine(root, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var items = string.Join("\n", models.Select(m => $"<Item><modelName>{m}</modelName></Item>"));
        File.WriteAllText(full, $"<CVehicleModelInfo__InitDataList><InitDatas>{items}</InitDatas></CVehicleModelInfo__InitDataList>");
    }
}
