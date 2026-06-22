#if CODEWALKER
using System.IO;
using System.Text;
using BCnEncoder.Decoder;
using BCnEncoder.Shared;
using CarForge.Core.Rendering;
using CodeWalker.GameFiles;
using CodeWalker.Utils;

namespace CarForge.App.Rendering;

/// <summary>
/// Loader real de .yft via CodeWalker.Core (geometria + textura).
/// Geometria de Fragment.Drawable + DrawableArray; textura embutida no
/// ShaderGroup, nos .ytd da pasta ou no vehshare; BC1–5 via CodeWalker, BC7 via
/// BCnEncoder. Rastreia de qual .ytd cada textura veio. Log em %TEMP%.
/// </summary>
public sealed class CodeWalkerModelLoader : IVehicleModelLoader
{
    private static readonly string LogPath =
        Path.Combine(Path.GetTempPath(), "carforge_viewer.log");

    private sealed class Diag
    {
        public int TexOk, TexFail, NoDiffuse, NoUv;
        public readonly Dictionary<string, int> Formats = new();
    }

    private readonly record struct NamedDict(string Name, TextureDictionary Dict);

    public ModelGeometry? Load(string yftPath)
    {
        var log = new StringBuilder(Path.GetFileName(yftPath));
        var diag = new Diag();
        try
        {
            var yft = new YftFile();
            yft.Load(File.ReadAllBytes(yftPath));
            var frag = yft.Fragment;
            log.Append($" frag={(frag != null)}");
            if (frag == null) return Done(log, null);

            var drawables = new List<DrawableBase>();
            if (frag.Drawable != null) drawables.Add(frag.Drawable);
            if (frag.DrawableArray?.data_items != null)
                foreach (var d in frag.DrawableArray.data_items)
                    if (d != null) drawables.Add(d);
            log.Append($" drawables={drawables.Count}");
            if (drawables.Count == 0) return Done(log, null);

            var dicts = new List<NamedDict>();
            foreach (var d in drawables)
                if (d.ShaderGroup?.TextureDictionary != null)
                    dicts.Add(new NamedDict("embutido", d.ShaderGroup.TextureDictionary));
            int embedded = dicts.Count;
            dicts.AddRange(LoadSiblingDicts(yftPath));
            var shared = LoadShared(yftPath);
            if (shared != null) dicts.Add(new NamedDict("vehshare", shared));
            log.Append($" dicts={dicts.Count}(emb={embedded},share={(shared != null)})");

            var parts = new List<ModelPart>();
            int modelsTotal = 0, geomsTotal = 0;
            foreach (var drawable in drawables)
            {
                var models = drawable.DrawableModels?.High
                          ?? drawable.DrawableModels?.Med
                          ?? drawable.DrawableModels?.Low
                          ?? drawable.AllModels;
                if (models == null) continue;
                modelsTotal += models.Length;
                foreach (var model in models)
                {
                    if (model?.Geometries == null) continue;
                    foreach (var geom in model.Geometries)
                    {
                        geomsTotal++;
                        try
                        {
                            var part = BuildPart(geom, dicts, diag);
                            if (part != null) parts.Add(part);
                        }
                        catch (Exception ex) { log.Append($" [geomErr:{ex.GetType().Name}]"); }
                    }
                }
            }

            log.Append($" models={modelsTotal} geoms={geomsTotal} parts={parts.Count}");
            log.Append($" texOk={diag.TexOk} texFail={diag.TexFail} noDiffuse={diag.NoDiffuse} noUv={diag.NoUv}");
            log.Append(" fmts=[" + string.Join(",", diag.Formats.Select(k => $"{k.Key}:{k.Value}")) + "]");

            return Done(log, parts.Count == 0 ? null
                : new ModelGeometry { ModelName = Path.GetFileNameWithoutExtension(yftPath), Parts = parts });
        }
        catch (Exception ex)
        {
            log.Append($" EXCEPTION:{ex.GetType().Name}:{ex.Message}");
            return Done(log, null);
        }
    }

    private static ModelGeometry? Done(StringBuilder log, ModelGeometry? result)
    {
        try { File.AppendAllText(LogPath, log.ToString() + Environment.NewLine); } catch { }
        return result;
    }

    private static readonly Dictionary<string, TextureDictionary?> SharedCache = new();

    private static TextureDictionary? LoadShared(string yftPath)
    {
        var dir = Path.GetDirectoryName(yftPath);
        for (int i = 0; i < 6 && dir != null; i++)
        {
            var p = Path.Combine(dir, "vehshare.ytd");
            if (File.Exists(p))
            {
                if (!SharedCache.TryGetValue(p, out var d))
                {
                    try { var y = new YtdFile(); y.Load(File.ReadAllBytes(p)); d = y.TextureDict; }
                    catch { d = null; }
                    SharedCache[p] = d;
                }
                return d;
            }
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    private static IEnumerable<NamedDict> LoadSiblingDicts(string yftPath)
    {
        var result = new List<NamedDict>();
        try
        {
            var dir = Path.GetDirectoryName(yftPath)!;
            var baseName = Path.GetFileNameWithoutExtension(yftPath);
            if (baseName.EndsWith("_hi", StringComparison.OrdinalIgnoreCase)) baseName = baseName[..^3];
            if (baseName.EndsWith("+hi", StringComparison.OrdinalIgnoreCase)) baseName = baseName[..^3];

            var exact = Path.Combine(dir, baseName + ".ytd");
            var ytds = new List<string>();
            if (File.Exists(exact)) ytds.Add(exact);
            var others = Directory.EnumerateFiles(dir, "*.ytd")
                .Where(p => !p.Equals(exact, StringComparison.OrdinalIgnoreCase)).ToList();
            if (others.Count <= 60) ytds.AddRange(others);

            foreach (var p in ytds)
            {
                try
                {
                    var ytd = new YtdFile();
                    ytd.Load(File.ReadAllBytes(p));
                    if (ytd.TextureDict != null) result.Add(new NamedDict(Path.GetFileName(p), ytd.TextureDict));
                }
                catch { }
            }
        }
        catch { }
        return result;
    }

    private static ModelPart? BuildPart(DrawableGeometry geom, List<NamedDict> dicts, Diag diag)
    {
        var vd = geom?.VertexData;
        var ib = geom?.IndexBuffer?.Indices;
        if (vd?.VertexBytes == null || ib == null) return null;

        int stride = vd.VertexStride, count = vd.VertexCount;
        if (stride < 12 || count <= 0) return null;
        var vb = vd.VertexBytes;

        var info = vd.Info;
        int uvOff = -1; var uvType = VertexComponentType.Nothing;
        if (info != null && ((info.Flags >> 6) & 1) == 1)
        {
            uvOff = info.GetComponentOffset(6);
            uvType = info.GetComponentType(6);
        }
        if (uvOff < 0) diag.NoUv++;

        var pos = new float[count * 3];
        var uvs = uvOff >= 0 ? new float[count * 2] : null;

        for (int i = 0; i < count; i++)
        {
            int off = i * stride;
            if (off + 12 > vb.Length) break;
            pos[i * 3] = BitConverter.ToSingle(vb, off);
            pos[i * 3 + 1] = BitConverter.ToSingle(vb, off + 4);
            pos[i * 3 + 2] = BitConverter.ToSingle(vb, off + 8);

            if (uvs != null)
            {
                int o = off + uvOff;
                if (uvType == VertexComponentType.Float2 && o + 8 <= vb.Length)
                {
                    uvs[i * 2] = BitConverter.ToSingle(vb, o);
                    uvs[i * 2 + 1] = BitConverter.ToSingle(vb, o + 4);
                }
                else if (uvType == VertexComponentType.Half2 && o + 4 <= vb.Length)
                {
                    uvs[i * 2] = (float)BitConverter.ToHalf(vb, o);
                    uvs[i * 2 + 1] = (float)BitConverter.ToHalf(vb, o + 2);
                }
            }
        }

        var indices = new int[ib.Length];
        for (int i = 0; i < ib.Length; i++) indices[i] = ib[i];

        byte[]? texBgra = null; int tw = 0, th = 0;
        var (tex, source) = ResolveDiffuse(geom!.Shader, dicts);
        if (tex == null) diag.NoDiffuse++;
        else
        {
            var fmt = tex.Format.ToString();
            diag.Formats[fmt] = diag.Formats.GetValueOrDefault(fmt) + 1;
            try
            {
                var px = GetPixelsBgra(tex);
                if (px is { Length: > 0 }) { texBgra = px; tw = tex.Width; th = tex.Height; diag.TexOk++; }
                else diag.TexFail++;
            }
            catch { diag.TexFail++; }
        }

        return new ModelPart
        {
            Positions = pos, Uvs = uvs, Indices = indices,
            TextureName = tex?.Name, TextureSource = source,
            TextureBgra = texBgra, TextureWidth = tw, TextureHeight = th,
        };
    }

    private static byte[]? GetPixelsBgra(Texture tex)
    {
        var px = DDSIO.GetPixels(tex, 0);
        if (px is { Length: > 0 }) return px;

        if (tex.Format == TextureFormat.D3DFMT_BC7 && tex.Data?.FullData != null)
        {
            int w = tex.Width, h = tex.Height;
            int size = ((w + 3) / 4) * ((h + 3) / 4) * 16;
            var full = tex.Data.FullData;
            if (full.Length < size) return null;
            var mip0 = new byte[size];
            Array.Copy(full, 0, mip0, 0, size);

            var colors = new BcDecoder().DecodeRaw(mip0, w, h, CompressionFormat.Bc7);
            var outp = new byte[w * h * 4];
            for (int i = 0; i < colors.Length && i * 4 + 3 < outp.Length; i++)
            {
                outp[i * 4] = colors[i].b;
                outp[i * 4 + 1] = colors[i].g;
                outp[i * 4 + 2] = colors[i].r;
                outp[i * 4 + 3] = colors[i].a;
            }
            return outp;
        }
        return null;
    }

    /// <summary>Acha a diffuse e resolve pra Texture com pixels, retornando a origem (.ytd).</summary>
    private static (Texture? Tex, string? Source) ResolveDiffuse(ShaderFX? shader, List<NamedDict> dicts)
    {
        var prms = shader?.ParametersList?.Parameters;
        var hashes = shader?.ParametersList?.Hashes;
        if (prms == null) return (null, null);

        TextureBase? chosen = null;
        for (int i = 0; i < prms.Length; i++)
        {
            if (prms[i].Data is not TextureBase tb) continue;
            chosen ??= tb;
            if (hashes != null && i < hashes.Length &&
                hashes[i].ToString().IndexOf("Diffuse", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                chosen = tb;
                break;
            }
        }
        if (chosen == null) return (null, null);
        if (chosen is Texture embedded && embedded.Data != null) return (embedded, "embutido");

        foreach (var nd in dicts)
        {
            var t = nd.Dict?.Lookup(chosen.NameHash);
            if (t?.Data != null) return (t, nd.Name);
        }
        return (null, null);
    }
}
#endif
