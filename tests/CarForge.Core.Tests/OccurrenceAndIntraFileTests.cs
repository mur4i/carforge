using CarForge.Core.Editing;
using CarForge.Core.Parsing;
using CarForge.Core.Scanning;
using Xunit;

namespace CarForge.Core.Tests;

/// <summary>
/// Cobre o caso "mesmo modelo declarado 2× no MESMO vehicles.meta" (ex.: o chiron
/// com áudio nero vs nero2): parse por bloco, detecção do que difere e remoção
/// cirúrgica mantendo só o bloco escolhido.
/// </summary>
public class OccurrenceAndIntraFileTests
{
    // dois blocos do mesmo carro no mesmo arquivo, diferindo só no audioNameHash
    private static string TwinChiron() => """
        <CVehicleModelInfo__InitDataList><InitDatas>
          <Item>
            <modelName>2019chiron</modelName>
            <handlingId>2019chiron</handlingId>
            <audioNameHash>nero</audioNameHash>
          </Item>
          <Item>
            <modelName>2019chiron</modelName>
            <handlingId>2019chiron</handlingId>
            <audioNameHash>nero2</audioNameHash>
          </Item>
        </InitDatas></CVehicleModelInfo__InitDataList>
        """;

    [Fact]
    public void ParseOccurrences_acha_dois_blocos_com_indices_e_hashes_distintos()
    {
        var root = Directory.CreateTempSubdirectory("cf_occ").FullName;
        var meta = Path.Combine(root, "data", "2019chiron", "vehicles.meta");
        Directory.CreateDirectory(Path.GetDirectoryName(meta)!);
        File.WriteAllText(meta, TwinChiron());

        var occ = MetaParsers.ParseOccurrences(meta).ToList();

        Assert.Equal(2, occ.Count);
        Assert.Equal(0, occ[0].BlockIndex);
        Assert.Equal(1, occ[1].BlockIndex);
        Assert.Equal("nero", occ[0].Fields["audionamehash"]);
        Assert.Equal("nero2", occ[1].Fields["audionamehash"]);
        Assert.NotEqual(occ[0].ContentHash, occ[1].ContentHash); // diferem -> "revisar"
    }

    [Fact]
    public void Scanner_registra_duas_ocorrencias_no_mesmo_arquivo()
    {
        var root = Directory.CreateTempSubdirectory("cf_occ2").FullName;
        var meta = Path.Combine(root, "data", "2019chiron", "vehicles.meta");
        Directory.CreateDirectory(Path.GetDirectoryName(meta)!);
        File.WriteAllText(meta, TwinChiron());

        var pack = new PackScanner().Scan(root);

        Assert.Equal(1, pack.UniqueModelCount);                  // 1 modelo único
        Assert.Equal(2, pack.Occurrences["2019chiron"].Count);   // 2 declarações
    }

    [Fact]
    public void PlanKeeping_mantem_o_bloco_escolhido_e_remove_o_outro_no_mesmo_arquivo()
    {
        var root = Directory.CreateTempSubdirectory("cf_occ3").FullName;
        var rel = Path.Combine("data", "2019chiron", "vehicles.meta");
        var meta = Path.Combine(root, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(meta)!);
        File.WriteAllText(meta, TwinChiron());

        var pack = new PackScanner().Scan(root);
        var remover = new DuplicateRemover();
        // mantém o bloco 0 (áudio nero), remove o bloco 1 (nero2)
        var plan = remover.PlanKeeping(pack, "2019chiron", rel, keepBlockIndex: 0);

        Assert.True(plan.IsSafe);
        remover.Apply(pack, plan);

        var txt = File.ReadAllText(meta);
        Assert.Contains("2019chiron", txt);   // o carro continua existindo
        Assert.Contains("nero", txt);          // mantido
        Assert.DoesNotContain("nero2", txt);   // removido
        Assert.Single(MetaParsers.ParseOccurrences(meta).ToList()); // sobrou 1 bloco
    }
}
