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

Está calibrado con un HUD en español a 1920×1080. F11 permite ajustar la zona y escala del aviso. Usa pantalla completa en ventana o modo ventana para ver el contador encima.

**Limitaciones:** cambios de HUD, avisos superpuestos o aperturas muy seguidas pueden causar omisiones. Los cofres de espíritus de 900 PE no están validados: súmalos manualmente si no se detectan. No hay detección independiente de las palancas traseras del mando.

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
