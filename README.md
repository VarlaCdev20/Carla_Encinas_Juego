# Carlita Mi Jueguito

Videojuego 2D de plataformas hecho en Unity 6.3 (Universal 2D) para la materia de Multimedia, siguiendo la guía *"Creando un videojuego 2D con Unity"* del Ing. Rubén Soria.

**Autora:** Carla Encinas

## Cómo jugar

| Acción | Tecla |
|---|---|
| Moverse | A / D o flechas |
| Saltar | Espacio |

Recolecta las abejas, pisa a los caracoles y evita a los puerquitos (y no te caigas al vacío).

## Recursos usados

- Sprites: [Legacy Fantasy - High Forest](https://anokolisa.itch.io/sidescroller-pixelart-sprites-asset-pack-forest-16x16) (anokolisa)
- Sonido: [Minifantasy Dungeon SFX y Music](https://leohpaz.itch.io/minifantasy-dungeon-sfx-pack) (Leohpaz)
- Botones: [UI Button Megapack](https://csmikelandre.itch.io/uibutton-megapack-84pngs) (csmikelandre)

## Plan de trabajo

- [x] 1. Crear el proyecto Universal 2D, configurar el input en "Both" y preparar el repositorio
- [x] 2. Importar los sprites y cortar los Tiles (16x16, Point)
- [x] 3. Armar la escena: personaje, cámara, fondo y piso con Tilemap
- [x] 4. Físicas: Rigidbody 2D, Capsule Collider 2D, material "solido" y Tilemap Collider 2D
- [x] 5. Script Jugador: movimiento, giro, salto (layer Pisito) y prefab del personaje
- [x] 6. Animaciones del personaje (Idle, Run, Jump, Jump-End) con PjController
- [x] 7. Cámara que sigue al personaje
- [x] 8. Abejas para recolectar y contador en pantalla (TextMeshPro)
- [x] 9. Puerquitos y zona de caída que reinician el nivel
- [x] 10. Caracoles que se pisan con rebote
- [x] 11. Música de fondo y efectos de sonido
- [x] 12. Menú principal con botones Jugar, Opciones y Salir
- [x] 13. Exportar el juego (Build)

## Exportar el juego

En Unity: **File > Build Profiles > Build** (o el menú **Juego > 3. Exportar juego**). El ejecutable de Linux queda en `Builds/Linux/CarlitaMiJueguito.x86_64`. La carpeta `Builds/` no se sube al repositorio.
