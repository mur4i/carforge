namespace CarForge.Core.Rendering;

/// <summary>Uma parte do modelo (uma geometria/shader) com sua textura própria.</summary>
public sealed class ModelPart
{
    /// <summary>Posições XYZ achatadas (x0,y0,z0, x1,...).</summary>
    public required float[] Positions { get; init; }
    /// <summary>UVs achatadas (u0,v0, u1,v1, ...). Mesmo nº de vértices de Positions/3. Opcional.</summary>
    public float[]? Uvs { get; init; }
    /// <summary>Índices dos triângulos.</summary>
    public required int[] Indices { get; init; }

    /// <summary>Nome da textura diffuse (pra ligar/desligar por textura). Opcional.</summary>
    public string? TextureName { get; init; }
    /// <summary>De onde a textura veio (nome do .ytd, "embutido" ou "vehshare"). Opcional.</summary>
    public string? TextureSource { get; init; }
    /// <summary>Pixels da textura diffuse em BGRA (largura*altura*4). Opcional.</summary>
    public byte[]? TextureBgra { get; init; }
    public int TextureWidth { get; init; }
    public int TextureHeight { get; init; }

    public bool HasTexture => TextureBgra is { Length: > 0 } && TextureWidth > 0 && TextureHeight > 0;
}

/// <summary>Geometria neutra de engine — o que um viewer precisa pra desenhar.</summary>
public sealed class ModelGeometry
{
    public required string ModelName { get; init; }
    public required List<ModelPart> Parts { get; init; }
}

/// <summary>
/// Contrato de carregamento de modelo. A UI (HelixToolkit) depende só disto,
/// não do CodeWalker. Implementações:
///   - NullVehicleModelLoader: placeholder (caixa), funciona já.
///   - CodeWalkerModelLoader: parseia .yft de verdade (com textura).
/// </summary>
public interface IVehicleModelLoader
{
    /// <summary>Carrega a geometria de um .yft. Retorna null se não suportado.</summary>
    ModelGeometry? Load(string yftPath);
}

/// <summary>Loader placeholder: devolve uma caixa com o nome do modelo. Sempre disponível.</summary>
public sealed class NullVehicleModelLoader : IVehicleModelLoader
{
    public ModelGeometry? Load(string yftPath)
    {
        var name = Path.GetFileNameWithoutExtension(yftPath);
        float[] p =
        {
            -1,-1,-1,  1,-1,-1,  1,1,-1,  -1,1,-1,
            -1,-1, 1,  1,-1, 1,  1,1, 1,  -1,1, 1,
        };
        int[] i =
        {
            0,1,2, 0,2,3,  4,6,5, 4,7,6,
            0,4,5, 0,5,1,  1,5,6, 1,6,2,
            2,6,7, 2,7,3,  3,7,4, 3,4,0,
        };
        return new ModelGeometry
        {
            ModelName = name,
            Parts = new List<ModelPart> { new() { Positions = p, Indices = i } },
        };
    }
}
