using System.Text;

namespace CarForge.Core.Generation;

/// <summary>
/// Gera um fxmanifest.lua válido pra um resource de veículo, escaneando os
/// arquivos presentes (data files de meta + audioconfig + sfx + stream).
/// </summary>
public sealed class ManifestGenerator
{
    public string Generate(string resourceRoot)
    {
        if (!Directory.Exists(resourceRoot))
            throw new DirectoryNotFoundException(resourceRoot);

        bool Has(string fileName) =>
            Directory.EnumerateFiles(resourceRoot, fileName, SearchOption.AllDirectories).Any();

        var sb = new StringBuilder();
        sb.AppendLine("-- Gerado por CarForge");
        sb.AppendLine("fx_version 'cerulean'");
        sb.AppendLine("game 'gta5'");
        sb.AppendLine("lua54 'yes'");
        sb.AppendLine();

        // files glob (mantém simples e abrangente)
        sb.AppendLine("files {");
        sb.AppendLine("    'data/**/*.meta',");
        sb.AppendLine("    'audioconfig/*.dat151',");
        sb.AppendLine("    'audioconfig/*.dat151.rel',");
        sb.AppendLine("    'audioconfig/*.dat151.nametable',");
        sb.AppendLine("    'audioconfig/*.dat54',");
        sb.AppendLine("    'audioconfig/*.dat54.rel',");
        sb.AppendLine("    'audioconfig/*.dat54.nametable',");
        sb.AppendLine("    'audioconfig/*.dat10',");
        sb.AppendLine("    'audioconfig/*.dat10.rel',");
        sb.AppendLine("    'audioconfig/*.dat10.nametable',");
        sb.AppendLine("    'sfx/**/*.awc',");
        sb.AppendLine("}");
        sb.AppendLine();

        // data_file por tipo de meta presente
        EmitMeta(sb, Has, "VEHICLE_METADATA_FILE", "vehicles.meta");
        EmitMeta(sb, Has, "HANDLING_FILE", "handling.meta");
        EmitMeta(sb, Has, "CARCOLS_FILE", "carcols.meta");
        EmitMeta(sb, Has, "VEHICLE_VARIATION_FILE", "carvariations.meta");
        EmitMeta(sb, Has, "CONTENT_UNLOCKING_META_FILE", "contentunlocks.meta");
        EmitMeta(sb, Has, "VEHICLE_LAYOUTS_FILE", "vehiclelayouts.meta");

        sb.AppendLine();
        // audio data files (descobre pelos pares *_game.dat / *_sounds.dat)
        var audioDir = Path.Combine(resourceRoot, "audioconfig");
        if (Directory.Exists(audioDir))
        {
            foreach (var game in Directory.EnumerateFiles(audioDir, "*_game.dat151.rel"))
            {
                var baseName = Path.GetFileName(game).Replace("_game.dat151.rel", "");
                sb.AppendLine($"data_file 'AUDIO_GAMEDATA' 'audioconfig/{baseName}_game.dat'");
                sb.AppendLine($"data_file 'AUDIO_SOUNDDATA' 'audioconfig/{baseName}_sounds.dat'");
                sb.AppendLine($"data_file 'AUDIO_WAVEPACK' 'sfx/dlc_{baseName}'");
            }
        }

        return sb.ToString();
    }

    public void Write(string resourceRoot, bool overwrite = false)
    {
        var path = Path.Combine(resourceRoot, "fxmanifest.lua");
        if (File.Exists(path) && !overwrite)
            throw new IOException($"fxmanifest.lua já existe (use overwrite): {path}");
        File.WriteAllText(path, Generate(resourceRoot));
    }

    private static void EmitMeta(StringBuilder sb, Func<string, bool> has, string token, string file)
    {
        if (has(file))
            sb.AppendLine($"data_file '{token}' 'data/**/{file}'");
    }
}
