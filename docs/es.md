# Menu — menú principal, elección de juego, partidas guardadas y ajustes

Menu es la primera pantalla que ves: el menú de título con **Jugar**, **Cargar**, **Ajustes**, **Mods y colecciones** y **Salir**. Desde ahí eliges uno de los juegos instalados, lo empiezas o lo continúas donde lo dejaste, cargas una partida guardada y cambias el idioma, el sonido y los ajustes de pantalla. Otros mods pueden añadir sus propios botones y páginas de ajustes.

Pertenece a la carcasa que siempre está cargada, así que está ahí antes de que empiece cualquier juego.

![El menú principal: Play, Load, Settings, Mods & Collections y Exit a la izquierda, el arte de portada de Cthangover a la derecha.](assets/main-menu.jpg)

## Empezar una partida

**Jugar** abre **Elige un juego**. Cada juego de tus colecciones es una tarjeta con su portada, descripción, estado de preparación y número de partidas guardadas.

![Dos tarjetas de juego en Choose a game: Cthangover y My Novel, cada una "Ready to play" y "No saves", con botones New game y Configure.](assets/game-picker.jpg)

- **Nueva partida** empieza el juego desde su primera escena. El menú cambia al conjunto de mods del juego en la misma ventana, sin reiniciar.
- **Continuar** en una tarjeta retoma la última partida guardada de ese juego; **Partidas** las enumera.
- **Instalar lo que falta** aparece cuando el juego necesita mods que no tienes y los descarga.
- **Configurar** abre el esquema del juego en el gestor de mods.

Mientras miras el menú, los mods de los juegos se preparan en segundo plano; la pista de abajo avisa de cuándo el arranque será rápido.

**Continuar** en el propio menú principal aparece cuando ya has jugado: te devuelve al último juego y partida que usaste.

## Cargar una partida guardada

**Cargar** muestra las partidas guardadas de un juego cada vez; elige el juego en el selector de arriba y haz clic en una partida para cargarla.

![La ventana de carga de My Novel con una partida, "Day 1, 00:00", y la pista "Click a save to load it".](assets/load.jpg)

Si los mods del juego han cambiado desde que se hizo la partida, una ventana **El juego ha cambiado** enumera lo que se añadió (empieza de cero), lo que se quitó (sus datos se conservan y vuelven si se activa de nuevo) y lo que se actualizó, y hace una copia de seguridad de la partida antes de cargarla. Las partidas de antes de que existieran los juegos aparecen en **Partidas sin juego**; eliges a qué juego pertenecen y el archivo se muda allí.

Dentro de un juego, guardar y cargar usan la misma ventana desde el menú del juego: haz clic en una ranura para guardar, una ranura usada se sobrescribe tras confirmar, y **+ Nueva partida guardada** añade una más.

## Ajustes

**Ajustes** tiene cuatro pestañas. Los cambios se aplican y se guardan al instante.

- **General** — el idioma de la interfaz.
- **Sonido** — activación y volumen de Sonidos, Música y Ambiente.
- **Pantalla** — modo de ventana (en ventana, maximizada, sin bordes o pantalla completa exclusiva), monitor, tamaño de la ventana, V-Sync y límite de fotogramas, además de **Restablecer ajustes de pantalla**.
- **Mods** — páginas de ajustes que añaden los mods instalados, con **Restablecer valores**.

![La pestaña de pantalla: modo de ventana, monitor, tamaño de la ventana, V-Sync y límite de fotogramas.](assets/settings-display.jpg)

![La pestaña Mods: la página del propio mod Menu con "Data of disabled mechanics" y un botón de purga.](assets/settings-mods.jpg)

La página del propio Menu en la pestaña **Mods** es **Datos de mecánicas desactivadas → Purgar**: quita de cada partida guardada los datos de los mods que ya no forman parte de los juegos. Úsala solo si estás seguro: una mecánica reactivada empieza de cero.

## De qué depende Menu

Menu dibuja las pantallas y decide qué hace cada botón. La lista de juegos y su preparación vienen de las colecciones; empezar, cambiar y guardar juegos lo hacen la sesión de juego y los servicios de guardado del motor; la música de la pantalla de título es la lista `MainMenu` de Music Pack, que Menu exige. **Mods y colecciones** lo aporta Mod Manager.

## Para autores de mods: añadir un botón o una página de ajustes

Los botones salen de un registro, no de una pantalla fija, así que un mod puede añadirlos, sustituirlos o reordenarlos. Mod Manager añade su botón con una entrada `main_menu` en su manifiesto:

```json
"main_menu": [
  {
    "id": "mods_hub",
    "scene_path": "scenes/mods_hub_screen.tscn",
    "button": { "icon": "mod_manager", "locale": "mod_manager/menu_title", "index": 100 }
  }
]
```

Los ajustes de un mod aparecen en la pestaña **Mods** cuando el mod registra definiciones de ajustes en el registro de ajustes de mods, como hace Menu con su acción de purga. Los textos de botones y ajustes salen de la localización del propio mod.

## Si algo falla

1. **No hay juegos bajo Jugar.** No hay ninguna colección de tipo *juego* instalada o activada; abre **Mods y colecciones**.
2. **Una tarjeta dice Faltan mods o Versiones incompatibles.** Usa **Instalar lo que falta** o **Configurar** para ver el problema.
3. **No aparece Continuar.** Aparece cuando has jugado al menos una vez.
4. **La ventana no mantiene su tamaño.** Revisa **Pantalla → Modo de ventana**; el tamaño de ventana solo se aplica en modo ventana.
