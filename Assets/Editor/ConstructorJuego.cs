using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

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
        // La muerte del caracol viene en una sola imagen: 8 cuadros de 48x32
        CortarHoja(Pack + "/Mob/Snail/Dead-Sheet.png", 48, 32, "Dead-Sheet", new Vector2(0.5f, 0f));
        AssetDatabase.SaveAssets();
        Debug.Log("[Constructor] Preparar OK");
    }

    static void CortarHoja(string ruta, int ancho, int alto, string prefijo, Vector2 pivote)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
        if (imp.spriteImportMode == SpriteImportMode.Multiple && imp.filterMode == FilterMode.Point)
            return; // ya estaba cortada
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
        CrearTag("abejita");
        CrearTag("puerquito");
        CrearTag("caracol");

        // Physics Material 2D "solido" con fricción 0
        var solido = new PhysicsMaterial2D("solido") { friction = 0f, bounciness = 0f };
        Guardar(solido, "Assets/solido.physicsMaterial2D");
        solido = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/solido.physicsMaterial2D");

        ConstruirEscenaJuego(solido);
        ConstruirEscenaMenu();

        // File > Build Profiles > Scene List: el menú primero
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/MenuPrincipal.unity", true),
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

        // Música de fondo en loop
        var musica = new GameObject("Musica").AddComponent<AudioSource>();
        musica.clip = Audio("Musica/Goblins_Dance_(Battle).wav");
        musica.loop = true;
        musica.playOnAwake = true;
        musica.volume = 0.3f;

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

        // Script Camara en la Main Camera con el personaje como Target
        cam.gameObject.AddComponent<Camara>().target = pj.transform;

        // Abejas para recolectar (x en casillas, y sobre el suelo)
        var prefAbeja = CrearAbeja();
        var abejas = new GameObject("Abejas");
        foreach (var (x, y) in PosAbejas)
            Colocar(prefAbeja, abejas.transform, x * Celda, SueloY + y);

        // Puerquitos que reinician el nivel
        var prefPuerquito = CrearPuerquito();
        var mobs = new GameObject("Mobs");
        foreach (var x in PosPuerquitos)
            Colocar(prefPuerquito, mobs.transform, x * Celda, SueloY);

        // Caracoles para pisar
        var prefCaracol = CrearCaracol();
        foreach (var x in PosCaracoles)
            Colocar(prefCaracol, mobs.transform, x * Celda, SueloY);

        // Zona invisible bajo el nivel: si el personaje cae, también reinicia (tag puerquito)
        var vacio = new GameObject("Vacio") { tag = "puerquito" };
        vacio.transform.position = new Vector3(55 * Celda, -2.5f, 0);
        var bv = vacio.AddComponent<BoxCollider2D>();
        bv.isTrigger = true;
        bv.size = new Vector2(160 * Celda, 0.5f);

        // UI: Marcador con la imagen de la abeja y el texto con la cantidad
        var canvas = NuevoCanvas();
        var marcador = new GameObject("Marcador", typeof(RectTransform), typeof(Image));
        marcador.transform.SetParent(canvas.transform, false);
        var img = marcador.GetComponent<Image>();
        img.sprite = prefAbeja.GetComponent<SpriteRenderer>().sprite;
        img.preserveAspect = true;
        Esquina(marcador.GetComponent<RectTransform>(), new Vector2(20, -20), new Vector2(120, 120));

        var txt = new GameObject("TxtAbejas", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        txt.transform.SetParent(canvas.transform, false);
        txt.text = "0";
        txt.fontSize = 64;
        txt.alignment = TextAlignmentOptions.MidlineLeft;
        Esquina(txt.rectTransform, new Vector2(150, -20), new Vector2(300, 120));
        pj.GetComponent<Jugador>().textoAbejas = txt;

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
    }

    // ---------------------------------------------------------- Menú
    static void ConstruirEscenaMenu()
    {
        const string ruta = "Assets/Scenes/MenuPrincipal.unity";
        File.Copy("Assets/Scenes/SampleScene.unity", ruta, true);
        AssetDatabase.ImportAsset(ruta);
        var escena = EditorSceneManager.OpenScene(ruta);
        var cam = Camera.main;
        var opciones = cam.gameObject.AddComponent<OpcionesMenu>();

        // Canvas con Reference Resolution 1920x1080 y Match 0.5
        var canvas = NuevoCanvas();
        var recursos = new TMP_DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
        };

        // Fondo del bosque
        var fondo = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
        fondo.transform.SetParent(canvas.transform, false);
        fondo.GetComponent<Image>().sprite = AssetDatabase.LoadAllAssetsAtPath(Pack + "/Background/Background.aseprite").OfType<Sprite>().First();
        Estirar(fondo.GetComponent<RectTransform>());

        var titulo = Texto(canvas.transform, "Titulo", "Carlita Mi Jueguito", 120, new Vector2(0, 320), new Vector2(1600, 170));
        titulo.fontStyle = FontStyles.Bold;
        titulo.color = new Color(0.27f, 0.17f, 0.13f);
        var autor = Texto(canvas.transform, "Autor", "por Carla Encinas", 50, new Vector2(0, 205), new Vector2(1000, 80));
        autor.color = new Color(0.27f, 0.17f, 0.13f);

        var jugar = Boton(canvas.transform, recursos, "Jugar", new Vector2(0, 40), "PLAY_BOTTOM_BOLD");
        var btnOpciones = Boton(canvas.transform, recursos, "Opciones", new Vector2(0, -110), "OPTIONS_BOTTOM_BOLD");
        var salir = Boton(canvas.transform, recursos, "Salir", new Vector2(0, -260), "EXIT_BOTTOM_BOLD");
        UnityEventTools.AddPersistentListener(jugar.onClick, opciones.vamosAjugar);
        UnityEventTools.AddPersistentListener(salir.onClick, opciones.salir);

        // Panelito: panel de opciones con su botón Cerrar, empieza desactivado
        var panelito = new GameObject("Panelito", typeof(RectTransform));
        panelito.transform.SetParent(canvas.transform, false);
        Estirar(panelito.GetComponent<RectTransform>());
        var panel = new GameObject("Panel de opciones", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(panelito.transform, false);
        panel.GetComponent<Image>().color = new Color(0.36f, 0.24f, 0.2f, 0.97f);
        panel.GetComponent<RectTransform>().sizeDelta = new Vector2(900, 620);
        Texto(panel.transform, "TxtOpciones", "Opciones", 72, new Vector2(0, 215), new Vector2(800, 100));
        Texto(panel.transform, "TxtControles",
            "Moverse: A / D o flechas\nSaltar: Espacio\n\nRecolecta todas las abejas,\npisa a los caracoles\ny cuidado con los puerquitos",
            40, new Vector2(0, 10), new Vector2(800, 320));
        var cerrar = Boton(panel.transform, recursos, "Cerrar", new Vector2(0, -215), "BLANK_BOND");
        UnityEventTools.AddBoolPersistentListener(btnOpciones.onClick, panelito.SetActive, true);
        UnityEventTools.AddBoolPersistentListener(cerrar.onClick, panelito.SetActive, false);
        panelito.SetActive(false);

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
    }

    // Imagen del pack de botones: _1 normal y _2 al pasar el mouse o presionar.
    // Las imágenes PLAY, OPTIONS y EXIT ya traen el texto; el botón en blanco usa el texto del botón.
    static Button Boton(Transform padre, TMP_DefaultControls.Resources recursos, string texto, Vector2 pos, string imagen)
    {
        var go = TMP_DefaultControls.CreateButton(recursos);
        go.name = "Boton" + texto;
        go.transform.SetParent(padre, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(514, 130);
        var tmp = go.GetComponentInChildren<TextMeshProUGUI>();
        tmp.text = texto;
        tmp.fontSize = 56;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        var boton = go.GetComponent<Button>();
        var img = go.GetComponent<Image>();
        img.sprite = SpriteBoton(imagen + "_1");
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
        boton.transition = Selectable.Transition.SpriteSwap;
        boton.spriteState = new SpriteState { highlightedSprite = SpriteBoton(imagen + "_2"), pressedSprite = SpriteBoton(imagen + "_2"), selectedSprite = img.sprite };
        tmp.gameObject.SetActive(imagen.StartsWith("BLANK"));
        return boton;
    }

    static Sprite SpriteBoton(string nombre)
    {
        string ruta = $"Assets/Sprites/Botones/{nombre}.png";
        var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
        if (imp.textureType != TextureImporterType.Sprite || imp.filterMode != FilterMode.Point)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
    }

    static TextMeshProUGUI Texto(Transform padre, string nombre, string texto, float tam, Vector2 pos, Vector2 caja)
    {
        var t = new GameObject(nombre, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(padre, false);
        t.text = texto;
        t.fontSize = tam;
        t.alignment = TextAlignmentOptions.Center;
        t.rectTransform.anchoredPosition = pos;
        t.rectTransform.sizeDelta = caja;
        return t;
    }

    static void Estirar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
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
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(dir + "/PjController.controller")
                   ?? CrearPjController(dir, animIdle, animRun, animJump, animJumpEnd);

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

        // Audio Source para los efectos de sonido
        go.AddComponent<AudioSource>().playOnAwake = false;

        var j = go.AddComponent<Jugador>();
        j.velocidad = 2f;
        j.comprobadorPiso = comprobador.transform;
        j.layerPiso = LayerMask.GetMask("Pisito");
        j.audioSource = go.GetComponent<AudioSource>();
        j.audioAbeja = Audio("Efectos/04_sack_open_2.wav");
        j.audioCaracol = Audio("Efectos/13_human_jump_land_1.wav");
        j.audioPuerquito = Audio("Efectos/14_human_death_spin.wav");

        return GuardarPrefab(go, "Assets/Prefab/Jugador.prefab");
    }

    static AnimatorController CrearPjController(string dir, AnimationClip animIdle, AnimationClip animRun, AnimationClip animJump, AnimationClip animJumpEnd)
    {
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
        return ctrl;
    }

    // ---------------------------------------------------------- Mobs
    static GameObject CrearAbeja()
    {
        var clip = ExportarClip(Pack + "/Mob/Small Bee/Fly/Fly.aseprite", "Assets/Animaciones/Abeja", "Fly", true);
        var go = Mob("Abeja", clip, "Assets/Animaciones/Abeja/Abeja.controller", "abejita");
        // Capsule Collider 2D con Is Trigger para recolectarla
        var cap = go.AddComponent<CapsuleCollider2D>();
        cap.isTrigger = true;
        cap.size = new Vector2(0.2f, 0.3f);
        cap.offset = go.GetComponent<SpriteRenderer>().sprite.bounds.center;
        return GuardarPrefab(go, "Assets/Prefab/Abeja.prefab");
    }

    static GameObject CrearPuerquito()
    {
        var clip = ExportarClip(Pack + "/Mob/Boar/Idle/Idle.aseprite", "Assets/Animaciones/Puerquito", "Idle", true);
        var go = Mob("Puerquito", clip, "Assets/Animaciones/Puerquito/Puerquito.controller", "puerquito");
        var cap = go.AddComponent<CapsuleCollider2D>();
        cap.isTrigger = true;
        cap.direction = CapsuleDirection2D.Horizontal;
        var b = go.GetComponent<SpriteRenderer>().sprite.bounds;
        cap.size = new Vector2(b.size.x - 0.06f, b.size.y - 0.05f);
        cap.offset = b.center;
        return GuardarPrefab(go, "Assets/Prefab/Puerquito.prefab");
    }

    static GameObject CrearCaracol()
    {
        // Animación Dead sin Loop Time; el Animator arranca apagado y se activa al pisarlo
        var cuadros = AssetDatabase.LoadAllAssetsAtPath(Pack + "/Mob/Snail/Dead-Sheet.png").OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Split('_')[1])).ToArray();
        var clip = ClipDeSprites(cuadros, "Assets/Animaciones/Caracol", "Dead", 12, false);
        var go = Mob("Caracol", clip, "Assets/Animaciones/Caracol/Caracol.controller", "caracol");
        go.GetComponent<Animator>().enabled = false;

        // Box Collider 2D en el cuerpo (dejando libre la cabeza)
        var box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(0.27f, 0.15f);
        box.offset = new Vector2(0, 0.075f);

        // Capsule Collider 2D horizontal encima, como trigger para el pisotón
        var cap = go.AddComponent<CapsuleCollider2D>();
        cap.isTrigger = true;
        cap.direction = CapsuleDirection2D.Horizontal;
        cap.size = new Vector2(0.27f, 0.08f);
        cap.offset = new Vector2(0, 0.19f);
        return GuardarPrefab(go, "Assets/Prefab/Caracol.prefab");
    }

    static GameObject Mob(string nombre, AnimationClip clip, string rutaCtrl, string tag)
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(rutaCtrl)
                   ?? AnimatorController.CreateAnimatorControllerAtPathWithClip(rutaCtrl, clip);
        var go = new GameObject(nombre) { tag = tag };
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = PrimerSprite(clip);
        sr.sortingOrder = 2;
        go.AddComponent<Animator>().runtimeAnimatorController = ctrl;
        return go;
    }

    static readonly (float, float)[] PosAbejas =
    {
        (-3, 0), (8, 0.6f), (18, 0), (35, 0.6f), (43, 0.95f), (52.5f, 0.45f), (62.5f, 0.6f),
        (70, 1.1f), (90, 0), (98, 0.6f), (107, 0.95f), (118, 0),
    };

    static readonly float[] PosCaracoles = { 13, 39, 66, 93, 120 };
    static readonly float[] PosPuerquitos = { 21, 47, 75, 113 };

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

    static void Colocar(GameObject prefab, Transform padre, float x, float y)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, padre);
        go.transform.position = new Vector3(x, y, 0);
    }

    // Canvas con UI Scale Mode "Scale With Screen Size"
    static Canvas NuevoCanvas()
    {
        var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.layer = LayerMask.NameToLayer("UI");
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        return canvas;
    }

    // Anclado a la esquina superior izquierda
    static void Esquina(RectTransform rt, Vector2 pos, Vector2 tam)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
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
        var ya = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{dir}/{nombre}.anim");
        if (ya != null) return ya;
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

    static AnimationClip ClipDeSprites(Sprite[] cuadros, string dir, string nombre, float fps, bool loop)
    {
        Carpeta(dir);
        var ya = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{dir}/{nombre}.anim");
        if (ya != null) return ya;
        var clip = new AnimationClip { frameRate = fps, name = nombre };
        var claves = cuadros.Select((s, i) => new ObjectReferenceKeyframe { time = i / fps, value = s }).ToArray();
        AnimationUtility.SetObjectReferenceCurve(clip,
            EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), claves);
        var ajustes = AnimationUtility.GetAnimationClipSettings(clip);
        ajustes.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, ajustes);
        Guardar(clip, $"{dir}/{nombre}.anim");
        return AssetDatabase.LoadAssetAtPath<AnimationClip>($"{dir}/{nombre}.anim");
    }

    static AudioClip Audio(string ruta) => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/" + ruta);

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
