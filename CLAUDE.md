# Pausas Frozz — notas para el asistente

Lee primero `DESARROLLO.md` (guía completa). Resumen de lo esencial:

- App WinForms para Windows, **C# 5 / .NET Framework 4** compilada con `build.ps1` (usa `csc.exe` de Windows). Prohibido usar sintaxis de C# 6+ (`$""`, `?.`, `nameof`, miembros `=>`).
- Archivos `.cs` en **UTF-8 con BOM**; textos de interfaz en español con tildes. En PowerShell 5 usar `[IO.File]::ReadAllText/WriteAllText` con `UTF8Encoding($true)`.
- **Publicar** = subir `AppInfo.Version` en `src/Program.cs` + PR a `main` (el título del PR = «Novedades» para los usuarios) + fusionar. `.github/workflows/release.yml` crea el Release; las apps instaladas se actualizan solas desde `releases/latest/download/`.
- No renombrar los assets del Release ni cambiar `AppInfo.Repo` (Frozz-Colombia/pausas-frozz); el repo debe seguir público.
- Probar siempre con `--demo ...` y `PAUSASFROZZ_DATA` apuntando a una carpeta temporal para no tocar la instalación real (instancia única, clave de inicio de Windows). `PAUSASFROZZ_DUMP=<carpeta>` guarda los cuadros de Frozzy para revisar animaciones.
- Frozzy es una marioneta de recortes (`src/fz_*.png`, generadas con `tools/piezas.cs`); ver constantes de articulaciones en `src/Frozzy.cs`. Verificar cambios visuales con capturas o volcado de cuadros antes de publicar.
- Commits y PRs sin menciones a herramientas de IA.
