using System;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

// Turns the art's 3x5 pixel font (PixelFont.png + PixelFont.json, exported by
// background_claude.lua) into a TextMeshPro font asset. Font size 7 draws each
// font pixel as one screen-layout pixel; use multiples of 7 for crisp text.
// Lowercase letters reuse the uppercase glyphs.
// Menu: Matcha > Build Pixel Font (Build Scene also runs it).
public static class PixelFontBuilder
{
    public const string Dir = "Assets/Art/Font";
    public const string AssetPath = Dir + "/PixelFont.asset";
    public const int PointSize = 7;   // 5px glyphs + 2px line gap

    [Serializable] class GlyphDef { public int code, x, y, w; }
    [Serializable] class FontDef { public int height; public GlyphDef[] glyphs; }

    [MenuItem("Matcha/Build Pixel Font")]
    public static TMP_FontAsset Build()
    {
        var def = JsonUtility.FromJson<FontDef>(File.ReadAllText(Dir + "/PixelFont.json"));
        var source = new Texture2D(2, 2);
        source.LoadImage(File.ReadAllBytes(Dir + "/PixelFont.png"));
        int width = source.width, height = source.height;

        // update in place when it exists so scenes keep their reference
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
        bool created = font == null;
        Texture2D atlas;
        Material material;
        if (created)
        {
            font = ScriptableObject.CreateInstance<TMP_FontAsset>();
            atlas = new Texture2D(width, height, TextureFormat.Alpha8, false) { name = "PixelFont Atlas" };
            material = new Material(Shader.Find("TextMeshPro/Mobile/Bitmap")) { name = "PixelFont Material" };
        }
        else
        {
            atlas = font.atlasTexture;
            material = font.material;
            atlas.Reinitialize(width, height);
        }
        atlas.filterMode = FilterMode.Point;
        atlas.wrapMode = TextureWrapMode.Clamp;
        atlas.SetPixels32(source.GetPixels32());
        atlas.Apply();
        UnityEngine.Object.DestroyImmediate(source);

        material.SetTexture(ShaderUtilities.ID_MainTex, atlas);
        material.SetFloat(ShaderUtilities.ID_TextureWidth, width);
        material.SetFloat(ShaderUtilities.ID_TextureHeight, height);

        // these setters are internal to TextMeshPro
        SetField(font, "m_Version", "1.1.0");
        SetProperty(font, "faceInfo", new FaceInfo
        {
            familyName = "Matcha Pixel",
            styleName = "Regular",
            pointSize = PointSize,
            scale = 1,
            lineHeight = PointSize,
            ascentLine = def.height + 1,
            capLine = def.height,
            meanLine = def.height,
            baseline = 0,
            descentLine = def.height + 1 - PointSize,
            underlineOffset = -1,
            underlineThickness = 1,
            strikethroughOffset = def.height / 2f,
            strikethroughThickness = 1,
            superscriptOffset = def.height,
            superscriptSize = 1,
            subscriptOffset = -1,
            subscriptSize = 1,
            tabWidth = 8,
        });
        SetProperty(font, "atlasPopulationMode", AtlasPopulationMode.Static);
        SetProperty(font, "atlasTextures", new[] { atlas });
        SetProperty(font, "atlasWidth", width);
        SetProperty(font, "atlasHeight", height);
        SetProperty(font, "atlasPadding", 0);
        SetProperty(font, "atlasRenderMode", GlyphRenderMode.RASTER);
        font.material = material;

        font.glyphTable.Clear();
        font.characterTable.Clear();
        for (int i = 0; i < def.glyphs.Length; i++)
        {
            var g = def.glyphs[i];
            // glyph rects count from the atlas' bottom-left; the json counts from the top-left
            var glyph = new Glyph((uint)i,
                new GlyphMetrics(g.w, def.height, 0, def.height, g.w + 1),
                new GlyphRect(g.x, height - g.y - def.height, g.w, def.height), 1, 0);
            font.glyphTable.Add(glyph);
            font.characterTable.Add(new TMP_Character((uint)g.code, font, glyph));
            if (g.code >= 'A' && g.code <= 'Z')
                font.characterTable.Add(new TMP_Character((uint)(g.code + 32), font, glyph));
        }
        font.ReadFontAssetDefinition();

        if (created)
        {
            AssetDatabase.CreateAsset(font, AssetPath);
            AssetDatabase.AddObjectToAsset(atlas, font);
            AssetDatabase.AddObjectToAsset(material, font);
        }
        EditorUtility.SetDirty(atlas);
        EditorUtility.SetDirty(material);
        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();
        Debug.Log($"[PixelFont] Built {AssetPath}: {def.glyphs.Length} glyphs, {font.characterTable.Count} characters");
        return font;
    }

    static void SetProperty(object target, string name, object value) =>
        typeof(TMP_FontAsset).GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(target, value);

    static void SetField(object target, string name, object value)
    {
        for (var t = target.GetType(); t != null; t = t.BaseType)
        {
            var field = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (field == null) continue;
            field.SetValue(target, value);
            return;
        }
        throw new MissingFieldException(target.GetType().Name, name);
    }
}
