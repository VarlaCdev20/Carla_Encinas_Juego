using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Hace que el menú principal sea lo primero que se ve en el editor:
// al darle Play siempre arranca desde MenuPrincipal (sin importar qué escena
// esté abierta) y al abrir el proyecto se abre MenuPrincipal en vez de SampleScene.
[InitializeOnLoad]
public static class InicioEnMenu
{
    const string RutaMenu = "Assets/Scenes/MenuPrincipal.unity";

    static InicioEnMenu()
    {
        var menu = AssetDatabase.LoadAssetAtPath<SceneAsset>(RutaMenu);
        if (menu == null) return;
        EditorSceneManager.playModeStartScene = menu;

        // Solo una vez por sesión, para no cambiar de escena en cada recompilación
        if (SessionState.GetBool("InicioEnMenu.Abierto", false)) return;
        SessionState.SetBool("InicioEnMenu.Abierto", true);
        EditorApplication.delayCall += () =>
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var activa = SceneManager.GetActiveScene();
            if (activa.path == RutaMenu || activa.isDirty) return;
            EditorSceneManager.OpenScene(RutaMenu);
        };
    }
}
