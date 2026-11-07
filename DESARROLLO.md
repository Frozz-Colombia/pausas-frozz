# Guía para desarrolladores — Pausas Frozz

App de bandeja para Windows con recordatorios SST (descanso visual 20-20-20, agua, estiramientos, pausa activa, postura) y la mascota **Frozzy**. Se distribuye como un solo `.exe` que se instala y **se actualiza solo** desde los Releases de este repositorio.

- **Repositorio:** https://github.com/Frozz-Colombia/pausas-frozz (debe seguir siendo **público**: las apps instaladas descargan las actualizaciones sin credenciales).
- **Instalador para usuarios:** https://github.com/Frozz-Colombia/pausas-frozz/releases/latest/download/PausasFrozz-Instalador.exe
- **Rama principal:** `main`. Todo lo que se fusiona en `main` con una versión nueva se publica a todos los equipos.

---

## 1. Preparar el equipo

Solo se necesita Windows 10/11 con .NET Framework 4.x (viene con Windows). **No hace falta Visual Studio**: se compila con el `csc.exe` que trae Windows.

```powershell
git clone https://github.com/Frozz-Colombia/pausas-frozz.git
cd pausas-frozz
powershell -ExecutionPolicy Bypass -File build.ps1    # genera PausasFrozz.exe en la raíz
```

Para publicar hace falta acceso de escritura a la organización **Frozz-Colombia** en GitHub (pedírselo al administrador) y, opcionalmente, la CLI `gh` autenticada (`gh auth login`).

## 2. Estructura

| Ruta | Qué es |
|---|---|
| `src/Program.cs` | Núcleo: bandeja, temporizador y reglas de los recordatorios, ventana de aviso, configuración, historial, **actualizador**, sonidos y voz. Aquí está `AppInfo.Version`. |
| `src/Frozzy.cs` | La mascota: ventana transparente, marioneta de recortes, animaciones por ejercicio, escritorio con logo y reloj, globos de diálogo. |
| `src/Instalador.cs` | El `.exe` se autoinstala si se abre fuera de `%LOCALAPPDATA%\PausasFrozz` (acceso directo, registro en *Aplicaciones*, desinstalación). |
| `src/Bienvenida.cs` | Ventana de bienvenida (nombre del usuario, voz). |
| `src/Frases.cs` | Frases motivacionales (`{n}` = ", Nombre" si el usuario lo autorizó). |
| `src/fz_*.png` | Piezas de Frozzy recortadas del diseño original (cabeza, torso, manga, pata, piernas, brazo del pulgar, logo). |
| `src/frozzy.png`, `src/instructivo.jpg`, `src/app.ico` | Ilustración original, instructivo SST e ícono. |
| `tools/piezas.cs` | Herramienta que genera `src/fz_*.png` a partir de `src/frozzy.png`. |
| `build.ps1` | Compila `PausasFrozz.exe` (incluye recursos y `System.Speech`). |
| `.github/workflows/release.yml` | Compila y publica el Release cuando cambia la versión en `main`. |
| `Instalar.cmd` | Instalador alternativo que descarga la última versión. |

## 3. Cómo publicar una actualización

1. Crear una rama desde `main` actualizado:
   ```powershell
   git checkout main; git pull
   git checkout -b mi-cambio
   ```
2. Hacer los cambios y compilar con `build.ps1` (debe decir `OK -> ...PausasFrozz.exe`).
3. Probar (ver sección 4).
4. **Subir el número de versión** en `src/Program.cs`:
   ```csharp
   public const string Version = "1.4.1";   // → "1.4.2" (arreglos) / "1.5.0" (funciones nuevas)
   ```
   Si no se sube la versión, **no se publica nada** (sirve para cambios de documentación).
5. Commit, push y Pull Request a `main`. **El título del PR es lo que verán los usuarios como «Novedades»** en el aviso de actualización, así que debe ser claro y en español.
6. Fusionar el PR. GitHub Actions (`release.yml`) compila y crea el Release `vX.Y.Z` con:
   `PausasFrozz.exe`, `PausasFrozz-Instalador.exe`, `version.txt` (versión + SHA-256 + novedades) e `Instalar.cmd`.
7. Verificar en *Actions* que el workflow terminó en verde y en *Releases* que quedó como **Latest**. Comprobación rápida:
   ```powershell
   curl.exe -fsSL https://github.com/Frozz-Colombia/pausas-frozz/releases/latest/download/version.txt
   ```

### Cómo se actualizan los equipos
La app revisa `releases/latest/download/version.txt` a los 45 s de arrancar y cada 6 h (solo en horario laboral y con la persona presente), o con clic derecho → *Buscar actualizaciones*. Si hay versión nueva pregunta; al aceptar descarga `PausasFrozz.exe`, **verifica el SHA-256**, renombra el exe en uso a `.old`, pone el nuevo y se reinicia con `--updated`. Si la versión nueva falla al arrancar, vuelve sola a la anterior.

**No renombrar** los assets del Release (`PausasFrozz.exe`, `version.txt`, `PausasFrozz-Instalador.exe`) ni cambiar `AppInfo.Repo` sin una transición: las apps instaladas dependen de esas rutas.

## 4. Probar sin afectar tu instalación

La app usa una instancia única y escribe la clave de inicio de Windows, así que para probar se usa el **modo demo** con datos aislados:

```powershell
$env:PAUSASFROZZ_DATA = "$env:TEMP\pausas_prueba"     # config/historial aislados
.\PausasFrozz.exe --demo frozzy          # Frozzy en el escritorio y a los 2,5 s lanza una alerta
.\PausasFrozz.exe --demo anim:marchar    # Frozzy de pie haciendo una animación
.\PausasFrozz.exe --demo idle:cafe       # acción de espera en el escritorio (cafe, celular, bostezo, teclear...)
.\PausasFrozz.exe --demo molesto         # reacción cuando lo molestan con el cursor
.\PausasFrozz.exe --demo vista           # aviso de un recordatorio (vista, agua, estiramiento, pausa, postura)
.\PausasFrozz.exe --demo config          # ventana de configuración
.\PausasFrozz.exe --demo bienvenida      # ventana de bienvenida
.\PausasFrozz.exe --portable             # correr sin instalar (ojo: comparte instancia con la app instalada)
```

Animaciones disponibles (`--demo anim:<nombre>`): `listo, alerta, celebrar, pulgar, saludar, postura, mirar_lejos, tomar_agua, cuello_der, cuello_izq, menton, hombros_circ, brazos_frente, brazos_arriba, giro_der, giro_izq, munecas, caminar, marchar, puntas, tobillos, cintura, respirar`.

**Depurar animaciones cuadro por cuadro:** con `$env:PAUSASFROZZ_DUMP = "C:\ruta\carpeta"` la app guarda cada cuadro de Frozzy como PNG (hasta 320). Útil para encontrar saltos o piezas despegadas.

Errores en tiempo de ejecución: `%APPDATA%\PausasFrozz\errores.log`.

## 5. Reglas del código (importante)

- **Solo C# 5** (el `csc.exe` de .NET Framework 4): nada de `$"..."`, `?.`, `nameof`, propiedades/métodos con `=>`, `out var`, etc. Usar `string +`, `delegate { }`, comprobaciones explícitas de null.
- Guardar los `.cs` en **UTF-8 con BOM** (los textos llevan tildes y `¡¿`). Si se editan con scripts de PowerShell 5, leer/escribir con `[IO.File]::ReadAllText/WriteAllText` y `UTF8Encoding($true)`; `Get-Content` sin `-Encoding` daña las tildes.
- Textos de la interfaz en **español** y tono Frozz.
- **Frozzy**: es una marioneta de recortes. Todas las piezas comparten el lienzo de 264×504 del diseño original; las articulaciones están en constantes de `Frozzy.cs` (`ShX/ShY` hombro, `ElX/ElY` codo, `NeckX/NeckY` cuello, `LegLX/LegRX/LegY` caderas). El brazo izquierdo es el derecho reflejado (`MirrorX`). Máscaras de hombro/codo/cadera rellenan los huecos al rotar. El personaje se dibuja a 2x y se reduce (supersampling); mantener `PixelOffsetMode.None` al dibujar las piezas (evita saltos de medio píxel).
- Para regenerar las piezas: `csc /r:System.Drawing.dll /out:piezas.exe tools\piezas.cs` y `piezas.exe src\frozzy.png <carpeta>`, luego copiar `p_*.png` como `src\fz_*.png`.
- Los commits y PRs no deben mencionar herramientas de IA.

## 6. Problemas conocidos

- **Smart App Control / "Windows protegió su PC"**: el `.exe` no está firmado. Algunos equipos con Smart App Control activado pueden bloquearlo (incluso una compilación local; recompilar cambia la huella). La solución de fondo es firmar el ejecutable (p. ej. Azure Trusted Signing) e integrarlo en `release.yml`.
- `release.yml` publica solo si la versión de `AppInfo.Version` no existe como Release; para volver a publicar hay que subir la versión.
