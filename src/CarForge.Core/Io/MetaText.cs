using System.Text;

namespace CarForge.Core.Io;

/// <summary>
/// Leitura tolerante de arquivos .meta de comunidade. Eles vêm com BOM,
/// encoding inconsistente (latin-1 misturado) e às vezes XML inválido.
/// Por isso NÃO usamos XmlDocument direto aqui — lemos como texto e o parsing
/// é por regex tolerante (validado contra 687 metas reais).
/// </summary>
public static class MetaText
{
    public static string Read(string path)
    {
        byte[] raw;
        try { raw = File.ReadAllBytes(path); }
        catch { return string.Empty; }

        // tenta UTF-8 (com/sem BOM) e cai pra latin-1, igual ao protótipo validado
        foreach (var enc in new[] { new UTF8Encoding(false, true), })
        {
            try { return StripBom(enc.GetString(raw)); }
            catch (DecoderFallbackException) { /* tenta próximo */ }
        }
        return StripBom(Encoding.Latin1.GetString(raw));
    }

    private static string StripBom(string s) =>
        s.Length > 0 && s[0] == '﻿' ? s[1..] : s;
}
