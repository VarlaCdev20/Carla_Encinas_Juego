using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class Jugador : MonoBehaviour
{
    public float velocidad = 2f;
    private Rigidbody2D rb;
    private float movimiento;
    public float alturaSalto = 4f;
    private bool esPiso;//true=estamos en el piso, false=estamos en el aire
    public Transform comprobadorPiso;
    public float radioComprobadorPiso = 0.1f;
    public LayerMask layerPiso;
    private Animator animator;
    private int cantAbejas = 0;
    public TMP_Text textoAbejas;
    private bool enRetroceso = false;
    private bool muerto = false;
    public float esperaTrasMorir = 0.6f; // segundos extra después de la animación Dead
    public AudioSource audioSource;
    public AudioClip audioPuerquito;
    public AudioClip audioCaracol;
    public AudioClip audioAbeja;
    public AudioClip audioAtaque;
    public AudioClip audioGolpe;
    public Transform puntoAtaque;
    public float radioAtaque = 0.25f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (muerto) return;
        if (!enRetroceso)
        {
            movimiento = Input.GetAxisRaw("Horizontal");
            rb.linearVelocity = new Vector2(movimiento * velocidad, rb.linearVelocity.y);
            if (movimiento != 0) transform.localScale = new Vector3(Mathf.Sign(movimiento), 1, 1);
        }
        if (Input.GetButtonDown("Jump") && esPiso)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, alturaSalto);
        if (Input.GetButtonDown("Fire1") && !enRetroceso)
            Atacar();
        animator.SetFloat("Velocidad", Mathf.Abs(movimiento));
        animator.SetFloat("VelocidadVertical", rb.linearVelocity.y);
        animator.SetBool("estaEnPiso", esPiso);
    }

    public void FixedUpdate()
    {
        esPiso = Physics2D.OverlapCircle(comprobadorPiso.position, radioComprobadorPiso, layerPiso);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (muerto) return;
        if (collision.transform.CompareTag("abejita"))
        {
            audioSource.PlayOneShot(audioAbeja);
            Destroy(collision.gameObject);
            cantAbejas++;
            textoAbejas.text = "" + cantAbejas;
        }
        if (collision.transform.CompareTag("puerquito"))
        {
            audioSource.PlayOneShot(audioPuerquito);
            Morir();
        }
        if (collision.transform.CompareTag("caracol"))
        {
            audioSource.PlayOneShot(audioCaracol);
            enRetroceso = true;
            Vector2 arrastre = (rb.position - (Vector2)collision.transform.position).normalized * 3;
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(arrastre, ForceMode2D.Impulse);
            Collider2D[] colliders = collision.GetComponents<Collider2D>();
            foreach (Collider2D col in colliders)
                col.enabled = false;
            collision.GetComponent<Animator>().enabled = true;
            Destroy(collision.gameObject, 0.4f);
            Invoke(nameof(QuitarRetroceso), 0.2f);
        }
    }

    // Golpe de espada: elimina a los puerquitos y caracoles que estén enfrente
    void Atacar()
    {
        animator.SetTrigger("Atacar");
        audioSource.PlayOneShot(audioAtaque);
        Collider2D[] golpeados = Physics2D.OverlapCircleAll(puntoAtaque.position, radioAtaque);
        foreach (Collider2D golpe in golpeados)
        {
            if (golpe.CompareTag("puerquito") && golpe.name != "Vacio")
            {
                audioSource.PlayOneShot(audioGolpe);
                Destroy(golpe.gameObject);
            }
            else if (golpe.CompareTag("caracol"))
            {
                audioSource.PlayOneShot(audioGolpe);
                foreach (Collider2D col in golpe.GetComponents<Collider2D>())
                    col.enabled = false;
                golpe.GetComponent<Animator>().enabled = true;
                Destroy(golpe.gameObject, 0.4f);
            }
        }
    }

    // Se queda quieto, reproduce la animación Dead y al terminar vuelve al menú
    void Morir()
    {
        muerto = true;
        enRetroceso = true;
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
        animator.SetFloat("Velocidad", 0);
        animator.SetFloat("VelocidadVertical", 0);
        animator.SetTrigger("Morir");
        float duracion = 1f;
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            if (clip.name == "Dead") duracion = clip.length;
        Invoke(nameof(VolverAlMenu), duracion + esperaTrasMorir);
    }

    void QuitarRetroceso()
    {
        enRetroceso = false;
    }

    // Al morir se vuelve al menú principal (escena 0 de la Scene List)
    void VolverAlMenu()
    {
        SceneManager.LoadScene(0);
    }
}
