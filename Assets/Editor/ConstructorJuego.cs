using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// Automatiza los pasos de la guía "Creando un videojuego 2D con Unity".
// Paso 1: Unity -batchmode -executeMethod ConstructorJuego.Preparar
// También se puede lanzar desde el menú "Juego" del editor.
public static class ConstructorJuego
{
    const string Pack = "Assets/Sprites/Legacy-Fantasy - High Forest 2.3";
    const string RutaTiles = Pack + "/Assets/Tiles.png";
    const int TamTile = 16;

    // ---------------------------------------------------------- Recursos
    [MenuItem("Juego/1. Preparar recursos")]
    public static void Preparar()
    {
        // Sprite Editor > Slice > Grid by cell size 16x16, Filter mode Point (no filter)
        CortarHoja(RutaTiles, TamTile, TamTile, "Tiles", new Vector2(0.5f, 0.5f));
        AssetDatabase.SaveAssets();
        Debug.Log("[Constructor] Preparar OK");
    }

    static void CortarHoja(string ruta, int ancho, int alto, string prefijo, Vector2 pivote)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Multiple;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.spritePixelsPerUnit = 100;
        imp.SaveAndReimport();

        var tex = new Texture2D(2, 2);
        tex.LoadImage(File.ReadAllBytes(ruta));
        int cols = tex.width / ancho, filas = tex.height / alto;

        var rects = new List<SpriteRect>();
        for (int f = 0; f < filas; f++)
            for (int c = 0; c < cols; c++)
            {
                var r = new RectInt(c * ancho, tex.height - (f + 1) * alto, ancho, alto);
                if (CeldaVacia(tex, r)) continue;
                rects.Add(new SpriteRect
                {
                    name = $"{prefijo}_{f * cols + c}",
                    rect = new Rect(r.x, r.y, r.width, r.height),
                    alignment = SpriteAlignment.Custom,
                    pivot = pivote,
                    spriteID = GUID.Generate()
                });
            }

        var fabricas = new SpriteDataProviderFactories();
        fabricas.Init();
        var dp = fabricas.GetSpriteEditorDataProviderFromObject(imp);
        dp.InitSpriteEditorDataProvider();
        dp.SetSpriteRects(rects.ToArray());
        var nombres = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
        nombres?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        dp.Apply();
        imp.SaveAndReimport();
        Debug.Log($"[Constructor] {prefijo} cortado: {rects.Count} sprites");
    }

    static bool CeldaVacia(Texture2D t, RectInt r)
    {
        for (int y = r.y; y < r.yMax; y++)
            for (int x = r.x; x < r.xMax; x++)
                if (t.GetPixel(x, y).a > 0.01f) return false;
        return true;
    }
}
