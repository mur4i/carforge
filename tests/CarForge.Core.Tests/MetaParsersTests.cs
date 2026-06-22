using CarForge.Core.Parsing;
using Xunit;

namespace CarForge.Core.Tests;

public class MetaParsersTests
{
    // estrutura real de carvariations.meta (Item de cor ANTES do bloco de kits),
    // que quebrava o protótipo regex ingênuo. O parser tem que achar o modkit.
    private const string CarVariations = """
        <CVehicleModelInfoVariation>
         <variationData>
          <Item>
           <modelName>1016rwdevo</modelName>
           <colors>
             <Item><indices content="char_array">1 3 5</indices></Item>
           </colors>
           <kits>
             <Item>7654851_1016rwdevo_modkit</Item>
           </kits>
          </Item>
          <Item>
           <modelName>asea</modelName>
           <kits>
             <Item>0_default_modkit</Item>
           </kits>
          </Item>
         </variationData>
        </CVehicleModelInfoVariation>
        """;

    private const string Vehicles = """
        <CVehicleModelInfo__InitDataList>
         <InitDatas>
          <Item>
            <modelName>350z</modelName>
            <handlingId>350z</handlingId>
            <audioNameHash>elegy</audioNameHash>
          </Item>
          <Item>
            <modelName>blista</modelName>
            <handlingId>BLISTA</handlingId>
          </Item>
         </InitDatas>
        </CVehicleModelInfo__InitDataList>
        """;

    [Fact]
    public void ParseVehicles_extrai_modelos_em_minusculo()
    {
        var file = Temp(Vehicles);
        var recs = MetaParsers.ParseVehicles(file).ToList();

        Assert.Equal(2, recs.Count);
        Assert.Contains(recs, r => r.Model == "350z" && r.HandlingId == "350z" && r.AudioHash == "elegy");
        Assert.Contains(recs, r => r.Model == "blista" && r.HandlingId == "blista");
    }

    [Fact]
    public void ParseModelKits_acha_modkit_real_e_ignora_default()
    {
        var file = Temp(CarVariations);
        var kits = MetaParsers.ParseModelKits(file).ToList();

        // 1016rwdevo tem modkit; asea só tem default (deve ser ignorado)
        Assert.Contains(kits, k => k.Model == "1016rwdevo" && k.Kit == "7654851_1016rwdevo_modkit");
        Assert.DoesNotContain(kits, k => k.Model == "asea");
    }

    private static string Temp(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"cf_{Guid.NewGuid():N}.meta");
        File.WriteAllText(path, content);
        return path;
    }
}
