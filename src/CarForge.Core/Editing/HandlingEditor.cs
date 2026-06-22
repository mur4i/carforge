using System.Text.RegularExpressions;
using CarForge.Core.Io;
using CarForge.Core.Models;

namespace CarForge.Core.Editing;

/// <summary>Uma entrada de handling (um carro) com seus campos numéricos.</summary>
public sealed class HandlingEntry
{
    public required string Name { get; init; }
    public required string FilePath { get; init; }
    /// <summary>campo (ex: fMass) → valor (string, como está no XML).</summary>
    public Dictionary<string, string> Fields { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string? Get(string field) => Fields.TryGetValue(field, out var v) ? v : null;
}

/// <summary>
/// Lê e edita handling.meta. Os campos são &lt;fNome value="x"/&gt;. Salvar é
/// um replace cirúrgico do atributo value dentro do &lt;Item&gt; daquele handlingName,
/// preservando todo o resto do arquivo.
/// </summary>
public sealed class HandlingEditor
{
    /// <summary>Campos mais usados (pra UI mostrar primeiro).</summary>
    public static readonly string[] CommonFields =
    {
        "fMass", "fInitialDriveForce", "fDriveInertia", "fInitialDriveMaxFlatVel",
        "fBrakeForce", "fHandBrakeForce", "fTractionCurveMax", "fTractionCurveMin",
        "fSteeringLock", "fSuspensionForce", "fSuspensionRaise",
    };

    private static readonly Regex ItemRx = new(@"<Item\b[^>]*>(?<body>.*?)</Item>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex NameRx = new(@"<handlingName>\s*([^<\s]+)\s*</handlingName>", RegexOptions.IgnoreCase);
    private static readonly Regex FieldRx = new(@"<(f[A-Za-z0-9]+)\s+value\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase);

    public List<HandlingEntry> Load(string handlingMetaPath)
    {
        var text = MetaText.Read(handlingMetaPath);
        var list = new List<HandlingEntry>();
        foreach (Match item in ItemRx.Matches(text))
        {
            var body = item.Groups["body"].Value;
            var nm = NameRx.Match(body);
            if (!nm.Success) continue;
            var entry = new HandlingEntry { Name = nm.Groups[1].Value, FilePath = handlingMetaPath };
            foreach (Match f in FieldRx.Matches(body))
                entry.Fields[f.Groups[1].Value] = f.Groups[2].Value;
            list.Add(entry);
        }
        return list;
    }

    public List<HandlingEntry> LoadPack(VehiclePack pack)
    {
        var all = new List<HandlingEntry>();
        foreach (var hm in Directory.EnumerateFiles(pack.RootPath, "handling.meta", SearchOption.AllDirectories))
            all.AddRange(Load(hm));
        return all;
    }

    /// <summary>Grava um novo valor de campo, mexendo só no Item daquele handlingName.</summary>
    public void SetField(HandlingEntry entry, string field, string value)
    {
        var text = MetaText.Read(entry.FilePath);

        var updated = ItemRx.Replace(text, m =>
        {
            var body = m.Groups["body"].Value;
            var nm = NameRx.Match(body);
            if (!nm.Success || !nm.Groups[1].Value.Equals(entry.Name, StringComparison.OrdinalIgnoreCase))
                return m.Value; // não é este carro

            var newBody = Regex.Replace(body,
                $@"(<{Regex.Escape(field)}\s+value\s*=\s*"")[^""]*("")",
                $"${{1}}{value}${{2}}", RegexOptions.IgnoreCase);
            return m.Value.Replace(body, newBody);
        });

        if (updated != text) File.WriteAllText(entry.FilePath, updated);
        entry.Fields[field] = value;
    }
}
