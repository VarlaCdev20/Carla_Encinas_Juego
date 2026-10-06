using UnityEngine;
using UnityEngine.SceneManagement;

// Escala el fondo (hijo de la cámara) para que siempre cubra toda la vista,
// sin importar el tamaño o la proporción de la ventana.
public class FondoCamara : MonoBehaviour
{
    private Camera cam;
    private SpriteRenderer sr;

    void Start()
    {
        cam = GetComponentInParent<Camera>();
        sr = GetComponent<SpriteRenderer>();
    }

    void LateUpdate()
    {
        float alto = cam.orthographicSize * 2f;
        float ancho = alto * cam.aspect;
        Vector2 tam = sr.sprite.bounds.size;
        float escala = Mathf.Max(ancho / tam.x, alto / tam.y) * 1.02f;
        transform.localScale = new Vector3(escala, escala, 1);
        Vector2 centro = sr.sprite.bounds.center * escala;
        transform.localPosition = new Vector3(-centro.x, -centro.y, transform.localPosition.z);
    }

    // Se agrega solo al fondo de la escena de juego (también al reiniciar el nivel)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Registrar()
    {
        SceneManager.sceneLoaded += (escena, modo) =>
        {
            var fondo = GameObject.Find("Background");
            if (fondo != null && fondo.GetComponent<FondoCamara>() == null && fondo.GetComponentInParent<Camera>() != null)
                fondo.AddComponent<FondoCamara>();
        };
    }
}
