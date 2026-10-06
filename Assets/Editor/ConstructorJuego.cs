using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
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
        Carpeta("Assets/Prefab");
        Carpeta("Assets/Animaciones");
        CrearLayer("Pisito");

        // Physics Material 2D "solido" con fricción 0
        var solido = new PhysicsMaterial2D("solido") { friction = 0f, bounciness = 0f };
        Guardar(solido, "Assets/solido.physicsMaterial2D");
        solido = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/solido.physicsMaterial2D");

        ConstruirEscenaJuego(solido);
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/Juego.unity", true),
        };
        AssetDatabase.SaveAssets();
        Debug.Log("[Constructor] Construir OK");
    }

    static void ConstruirEscenaJuego(PhysicsMaterial2D solido)
    {
        const string ruta = "Assets/Scenes/Juego.unity";
        File.Copy("Assets/Scenes/SampleScene.unity", ruta, true);
        AssetDatabase.ImportAsset(ruta);
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
        piso.gameObject.layer = LayerMask.NameToLayer("Pisito");
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

        // Colisión del piso y paredes invisibles en los extremos del nivel
        piso.gameObject.AddComponent<TilemapCollider2D>();
        Pared(piso.gameObject, (Nivel[0].Item1 - 0.5f) * Celda);
        Pared(piso.gameObject, (Nivel[3].Item2 + 1.5f) * Celda);

        // Personaje desde su prefab
        var pj = (GameObject)PrefabUtility.InstantiatePrefab(CrearJugador(solido));
        pj.transform.position = new Vector3(InicioX * Celda, SueloY + 0.1f, 0);
        cam.transform.position = new Vector3(pj.transform.position.x, pj.transform.position.y, -10);

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
    }

    // ---------------------------------------------------------- Personaje
    static GameObject CrearJugador(PhysicsMaterial2D solido)
    {
        // Animaciones exportadas del pack a Animaciones/Jugador
        string dir = "Assets/Animaciones/Jugador";
        var animIdle = ExportarClip(Pack + "/Character/Idle/Idle.aseprite", dir, "Idle", true);
        var animRun = ExportarClip(Pack + "/Character/Run/Run.aseprite", dir, "Run", true);
        var animJump = ExportarClip(Pack + "/Character/Jump/Jump.aseprite", dir, "Jump", false);
        var animJumpEnd = ExportarClip(Pack + "/Character/Jump-End/Jump-End.aseprite", dir, "Jump-End", false);

        // PjController: parámetros Velocidad, VelocidadVertical y estaEnPiso
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(dir + "/PjController.controller");
        ctrl.AddParameter("Velocidad", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("VelocidadVertical", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("estaEnPiso", AnimatorControllerParameterType.Bool);
        var sm = ctrl.layers[0].stateMachine;
        var sIdle = sm.AddState("Idle", new Vector3(300, 0));
        var sRun = sm.AddState("Run", new Vector3(300, 120));
        var sJump = sm.AddState("Jump", new Vector3(600, 0));
        var sJumpEnd = sm.AddState("Jump-End", new Vector3(600, 120));
        sIdle.motion = animIdle; sRun.motion = animRun; sJump.motion = animJump; sJumpEnd.motion = animJumpEnd;
        sm.defaultState = sIdle;

        // Sin Has Exit Time y con Transition Duration 0
        Transicion(sIdle, sRun, ("Velocidad", AnimatorConditionMode.Greater, 0.1f));
        Transicion(sRun, sIdle, ("Velocidad", AnimatorConditionMode.Less, 0.1f));
        var anyJump = sm.AddAnyStateTransition(sJump);
        anyJump.hasExitTime = false; anyJump.duration = 0; anyJump.canTransitionToSelf = false;
        anyJump.AddCondition(AnimatorConditionMode.Greater, 0.1f, "VelocidadVertical");
        anyJump.AddCondition(AnimatorConditionMode.IfNot, 0, "estaEnPiso");
        Transicion(sJump, sJumpEnd, ("VelocidadVertical", AnimatorConditionMode.Less, 0.1f));
        Transicion(sJumpEnd, sRun, ("Velocidad", AnimatorConditionMode.Greater, 0.1f), ("estaEnPiso", AnimatorConditionMode.If, 0));
        Transicion(sJumpEnd, sIdle, ("Velocidad", AnimatorConditionMode.Less, 0.1f), ("estaEnPiso", AnimatorConditionMode.If, 0));
        // Caer de un borde sin saltar también usa la animación de caída
        Transicion(sIdle, sJumpEnd, ("VelocidadVertical", AnimatorConditionMode.Less, -0.5f), ("estaEnPiso", AnimatorConditionMode.IfNot, 0));
        Transicion(sRun, sJumpEnd, ("VelocidadVertical", AnimatorConditionMode.Less, -0.5f), ("estaEnPiso", AnimatorConditionMode.IfNot, 0));

        // Sprite Idle, por encima del fondo con Order in Layer 2
        var idle = PrimerSprite(animIdle);
        var go = new GameObject("Idle");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = idle;
        sr.sortingOrder = 2;
        go.AddComponent<Animator>().runtimeAnimatorController = ctrl;

        // Rigidbody 2D: Continuous, Interpolate y Freeze Rotation en Z
        var rb = go.AddComponent<Rigidbody2D>();
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        // Capsule Collider 2D con el material "solido"
        var cap = go.AddComponent<CapsuleCollider2D>();
        var b = idle.bounds;
        cap.size = new Vector2(0.24f, b.size.y - 0.02f);
        cap.offset = b.center;
        cap.sharedMaterial = solido;

        // ComprobadorDePiso en los pies del personaje
        var comprobador = new GameObject("ComprobadorDePiso");
        comprobador.transform.SetParent(go.transform, false);
        comprobador.transform.localPosition = new Vector3(b.center.x, b.min.y, 0);

        var j = go.AddComponent<Jugador>();
        j.velocidad = 2f;
        j.comprobadorPiso = comprobador.transform;
        j.layerPiso = LayerMask.GetMask("Pisito");

        return GuardarPrefab(go, "Assets/Prefab/Jugador.prefab");
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

    static void Pared(GameObject piso, float x)
    {
        var box = piso.AddComponent<BoxCollider2D>();
        box.offset = new Vector2(x, 1f);
        box.size = new Vector2(Celda, 6f);
    }

    // Copia la animación que genera el importador de Aseprite a Animaciones/<carpeta>
    static AnimationClip ExportarClip(string rutaAse, string dir, string nombre, bool loop)
    {
        Carpeta(dir);
        var original = AssetDatabase.LoadAllAssetsAtPath(rutaAse).OfType<AnimationClip>().First();
        var clip = new AnimationClip { frameRate = original.frameRate, name = nombre };
        foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(original))
        {
            if (b.type != typeof(SpriteRenderer)) continue;
            var curva = AnimationUtility.GetObjectReferenceCurve(original, b);
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), curva);
            break;
        }
        var ajustes = AnimationUtility.GetAnimationClipSettings(clip);
        ajustes.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, ajustes);
        Guardar(clip, $"{dir}/{nombre}.anim");
        return AssetDatabase.LoadAssetAtPath<AnimationClip>($"{dir}/{nombre}.anim");
    }

    static Sprite PrimerSprite(AnimationClip clip) =>
        (Sprite)AnimationUtility.GetObjectReferenceCurve(clip, AnimationUtility.GetObjectReferenceCurveBindings(clip)[0])[0].value;

    static void Transicion(AnimatorState de, AnimatorState a, params (string p, AnimatorConditionMode m, float v)[] conds)
    {
        var t = de.AddTransition(a);
        t.hasExitTime = false;
        t.duration = 0;
        foreach (var c in conds) t.AddCondition(c.m, c.v, c.p);
    }

    static GameObject GuardarPrefab(GameObject go, string ruta)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, ruta);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // Project Settings > Tags and Layers
    static void CrearLayer(string nombre)
    {
        if (LayerMask.NameToLayer(nombre) >= 0) return;
        var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tm.FindProperty("layers");
        for (int i = 6; i < layers.arraySize; i++)
            if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
            {
                layers.GetArrayElementAtIndex(i).stringValue = nombre;
                tm.ApplyModifiedProperties();
                return;
            }
    }

    static void CrearTag(string nombre)
    {
        if (UnityEditorInternal.InternalEditorUtility.tags.Contains(nombre)) return;
        UnityEditorInternal.InternalEditorUtility.AddTag(nombre);
    }

    // Si el asset ya existe se sobrescribe su contenido para conservar el GUID
    static void Guardar(Object obj, string ruta)
    {
        var existente = AssetDatabase.LoadMainAssetAtPath(ruta);
        if (existente != null && existente.GetType() == obj.GetType())
        {
            EditorUtility.CopySerialized(obj, existente);
            EditorUtility.SetDirty(existente);
            AssetDatabase.SaveAssetIfDirty(existente);
            return;
        }
        AssetDatabase.DeleteAsset(ruta);
        AssetDatabase.CreateAsset(obj, ruta);
    }

    static void Carpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        var padre = Path.GetDirectoryName(ruta).Replace('\\', '/');
        Carpeta(padre);
        AssetDatabase.CreateFolder(padre, Path.GetFileName(ruta));
    }
}
