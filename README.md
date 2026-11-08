# MisCofres

Contador de cofres para Fortnite en Windows: un cofre sin fondo, el total debajo y ajustes personales con **F11**.

## Descargar y abrir

Descarga **MisCofres.exe** desde [Releases](https://github.com/WladimirPro/MisCofres/releases/latest). Basta con ese archivo; la imagen y el icono están integrados. No requiere instalación.

El ejecutable no tiene firma digital. Windows o un antivirus pueden bloquearlo por reputación o por una detección que debe revisarse. Este repositorio no garantiza que un aviso de seguridad sea un falso positivo. Si ocurre, informa del texto exacto, antivirus, versión y nombre de la detección en una incidencia.

## Progreso y controles

- El total se guarda inmediatamente después de cada cambio, fuera de la carpeta del programa.
- Cerrar la app, reiniciar Windows o reemplazar el ejecutable no reinicia el progreso.
- Solo **Reiniciar total…** y una confirmación explícita ponen el total a cero. La confirmación selecciona **No** por defecto.
- Cada usuario de Windows tiene su propio progreso y sus propios ajustes.
- **Lateral superior del mouse:** sumar manualmente, configurable en F11.
- **F9:** restar uno; **F10:** mostrar u ocultar; **F11:** ajustes.
- **Ctrl+F10:** activar o terminar el modo de arrastrar el contador.
- Doble clic en el icono junto al reloj o volver a abrir la app: abrir ajustes.

Los datos están en `%LOCALAPPDATA%\MisCofres`. `total.txt` guarda el total y `total.txt.bak` conserva el valor anterior. `ajustes.xml` guarda las preferencias. Conserva una copia de esa carpeta si cambias de PC. Esos datos no se incluyen en las descargas.

## Automático local

El detector compara la palabra **COFRE** del aviso de experiencia con una plantilla visual. Solo observa una zona del juego cuando Fortnite está en primer plano. No usa IA, APIs, servidores ni uso de ChatGPT; tampoco lee memoria del juego ni pulsa controles.

Desde v1.1, **Adaptar automáticamente al HUD y a la resolución** está activado por defecto en F11 → Automático, también al cargar los ajustes de v1.0. Busca grupos de letras de varios tamaños en una zona más amplia y compara su forma normalizada con COFRE. La escala se calcula a partir de la altura de la ventana para conservar las proporciones en monitores ultrapanorámicos. La app solicita coordenadas físicas para evitar depender de la escala de Windows.

Si necesitas el detector anterior, desactiva esa opción y ajusta la zona y escala manuales. Usa pantalla completa en ventana o modo ventana para ver el contador encima.

**Limitaciones:** no garantiza todos los HUD. Sigue dependiendo del texto COFRE en español; textos demasiado pequeños, otra fuente, avisos tapados o aperturas muy seguidas pueden causar omisiones. Los cofres de espíritus de 900 PE no están validados: súmalos manualmente si no se detectan. No hay detección independiente de las palancas traseras del mando.

Validación de v1.1: pruebas con un recorte real del aviso reescalado a 1–3 veces el tamaño de referencia, normalización de 1080p/1440p/4K/ultrapanorámico y rechazo de un aviso de escudo roto. Además se revisaron 213 capturas locales de calibración, con una apertura positiva y ninguna falsa apertura en las restantes. Son pruebas de imagen y de lógica; la compatibilidad con cada HUD real debe comprobarse en el juego.

En v1.1.1 se añadió una muestra **real del HUD al 70**, cuyo dibujo de letras difería del HUD al 50 y no se reproducía simplemente reescalando la primera muestra. La secuencia real de 449 capturas cuenta una apertura, sin duplicados. Se mantuvo el umbral de detección y se volvieron a comprobar las capturas negativas. Esto corrige el caso observado; otros HUD todavía requieren validación real.

El programa conserva localmente un diagnóstico y, como máximo, la última imagen pequeña de un aviso reconocido. No graba secuencias ni transmite capturas.

## Compilar y comprobar

En Windows con .NET Framework y su compilador C#:

```powershell
.\build.ps1
```

El script compila el ejecutable, ejecuta pruebas de persistencia, ajustes y entradas, y genera `dist/MisCofres.exe`, `dist/MisCofres.zip` y `dist/SHA256SUMS.txt`. Las pruebas usan carpetas temporales aisladas, sin modificar el progreso real. `-SkipTests` omite las pruebas.

## Imágenes

Proyecto no oficial, sin afiliación con Epic Games. La imagen del cofre pertenece a Fortnite / Epic Games y procede de [Fortnite Wiki](https://fortnite.weirdgloop.org/w/Chest). Los derechos de la imagen y las marcas corresponden a sus titulares.

La primera publicación no incluye una licencia adicional para el código ni concede derechos sobre esos recursos de terceros.
