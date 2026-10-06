using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

// Automatiza los pasos de la guía "Creando un videojuego 2D con Unity".
// Paso 1: Unity -batchmode -executeMethod ConstructorJuego.Preparar
// Paso 2: Unity -batchmode -executeMethod ConstructorJuego.Construir
// También se puede lanzar desde el menú "Juego" del editor.
public static class ConstructorJuego
{
    const string Pack = "Assets/Sprites/Legacy-Fantasy - High Forest 2.3";
    const string RutaTiles = Pack + "/Assets/Tiles.png";
    const int TamTile = 16;
    const float Celda = 0.16f;

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

    // ---------------------------------------------------------- Escenas
    [MenuItem("Juego/2. Construir escenas")]
    public static void Construir()
    {
        Carpeta("Assets/Tiles");
        ConstruirEscenaJuego();
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/Juego.unity", true),
        };
        AssetDatabase.SaveAssets();
        Debug.Log("[Constructor] Construir OK");
    }

    static void ConstruirEscenaJuego()
    {
        const string ruta = "Assets/Scenes/Juego.unity";
        AssetDatabase.DeleteAsset(ruta);
        AssetDatabase.CopyAsset("Assets/Scenes/SampleScene.unity", ruta);
        var escena = EditorSceneManager.OpenScene(ruta);

        // Main Camera: Projection Size = 1.5
        var cam = Camera.main;
        cam.orthographicSize = 1.5f;
        cam.backgroundColor = new Color(0.1f, 0.14f, 0.16f);

        // Fondo: hijo de la cámara para que siempre cubra la pantalla
        var fondoSprite = AssetDatabase.LoadAllAssetsAtPath(Pack + "/Background/Background.aseprite").OfType<Sprite>().First();
        var fondo = new GameObject("Background");
        var fsr = fondo.AddComponent<SpriteRenderer>();
        fsr.sprite = fondoSprite;
        fsr.sortingOrder = -10;
        fondo.transform.SetParent(cam.transform, false);
        float escala = 3.2f / fondoSprite.bounds.size.y;
        fondo.transform.localPosition = new Vector3(-fondoSprite.bounds.center.x * escala, -fondoSprite.bounds.center.y * escala, 10);
        fondo.transform.localScale = new Vector3(escala, escala, 1);
        fondo.AddComponent<FondoCamara>();

        // Grid con Cell Size 0.16 y Tilemap "Piso"
        var grid = new GameObject("Grid").AddComponent<Grid>();
        grid.cellSize = new Vector3(Celda, Celda, 0);
        var piso = NuevoTilemap(grid, "Piso", 0);
        var deco = NuevoTilemap(grid, "Decoracion", 1);

        // Bloque de pasto (5x5 arriba a la izquierda de la hoja de Tiles)
        void Plataforma(int x0, int x1, int ySup, int prof)
        {
            for (int x = x0; x <= x1; x++)
            {
                int col = x == x0 ? 0 : x == x1 ? 4 : 1 + (x - x0 - 1) % 3;
                deco.SetTile(new Vector3Int(x, ySup + 1, 0), Tile(0, col));
                piso.SetTile(new Vector3Int(x, ySup, 0), Tile(1, col));
                for (int y = ySup - 1; y > ySup - prof; y--)
                    piso.SetTile(new Vector3Int(x, y, 0), Tile(2 + (ySup - 1 - y) % 2, col));
                piso.SetTile(new Vector3Int(x, ySup - prof, 0), Tile(4, col));
            }
        }
        foreach (var (x0, x1, y, prof) in Nivel) Plataforma(x0, x1, y, prof);

        // Personaje (sprite Idle), por encima del fondo con Order in Layer 2
        var idle = AssetDatabase.LoadAllAssetsAtPath(Pack + "/Character/Idle/Idle.aseprite").OfType<Sprite>().First();
        var pj = new GameObject("Idle");
        var sr = pj.AddComponent<SpriteRenderer>();
        sr.sprite = idle;
        sr.sortingOrder = 2;
        pj.transform.position = new Vector3(InicioX * Celda, SueloY + 0.1f, 0);
        cam.transform.position = new Vector3(pj.transform.position.x, pj.transform.position.y, -10);

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
    }

    // Tramos de piso (x inicial, x final, fila superior, profundidad) en casillas de 16 px
    static readonly (int, int, int, int)[] Nivel =
    {
        (-14, 24, -3, 5), (29, 50, -3, 5), (55, 78, -3, 5), (83, 124, -3, 5), // piso con huecos
        (6, 10, 1, 1), (33, 37, 1, 1), (41, 45, 3, 1),                       // plataformas flotantes
        (60, 65, 1, 1), (68, 72, 4, 1), (95, 101, 1, 1), (105, 109, 3, 1),
    };
    const int InicioX = -9;
    const float SueloY = -2 * Celda; // borde superior de la fila -3

    // ---------------------------------------------------------- Utilidades
    static readonly Dictionary<string, TileBase> cacheTiles = new();
    static TileBase Tile(int fila, int col)
    {
        string nombre = $"Tiles_{fila * 25 + col}";
        if (cacheTiles.TryGetValue(nombre, out var t)) return t;
        string ruta = $"Assets/Tiles/{nombre}.asset";
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(ruta);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = AssetDatabase.LoadAllAssetsAtPath(RutaTiles).OfType<Sprite>().First(s => s.name == nombre);
            AssetDatabase.CreateAsset(tile, ruta);
        }
        return cacheTiles[nombre] = tile;
    }

    static Tilemap NuevoTilemap(Grid grid, string nombre, int orden)
    {
        var go = new GameObject(nombre, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(grid.transform, false);
        go.GetComponent<TilemapRenderer>().sortingOrder = orden;
        return go.GetComponent<Tilemap>();
    }

    static void Carpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        var padre = Path.GetDirectoryName(ruta).Replace('\\', '/');
        Carpeta(padre);
        AssetDatabase.CreateFolder(padre, Path.GetFileName(ruta));
    }
}
