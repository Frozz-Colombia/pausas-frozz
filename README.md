# Pausas Frozz

Recordatorios en segundo plano basados en el instructivo SST **«Ergonomía en puestos administrativos y uso de pantallas»** de Frozz Colombia.

| Recordatorio | Cada (por defecto) | Qué hace |
|---|---|---|
| Descanso visual 20-20-20 | 20 min | Mirar a 6 m durante 20 s, con cuenta regresiva |
| Hidratación | 60 min | Tomar un vaso de agua |
| Estiramientos cuello, hombros, espalda y muñecas | 60 min | Rutina guiada de 9 pasos (~2 min) |
| Pausa activa | 90 min | Levantarse y moverse, 6 pasos (~3 min) |
| Revisión de postura | 120 min | Lista de verificación del instructivo |

## Instalar (cada usuario)
Descargar **[PausasFrozz-Instalador.exe](https://github.com/smedina-cpu/pausas-frozz/releases/latest/download/PausasFrozz-Instalador.exe)**, abrirlo y dar clic en **Instalar**.

- Se instala solo para el usuario, sin permisos de administrador, en `%LOCALAPPDATA%\PausasFrozz`.
- Crea el acceso directo en el menú Inicio, arranca con Windows y aparece en *Configuración › Aplicaciones* (desde ahí se desinstala).
- Después de instalar, el archivo descargado se puede borrar.
- Alternativa: [Instalar.cmd](https://github.com/smedina-cpu/pausas-frozz/releases/latest/download/Instalar.cmd) descarga e instala la última versión.

> Como el .exe no tiene firma digital, Windows puede mostrar *«Windows protegió su PC»* la primera vez: clic en **Más información → Ejecutar de todas formas**.

## Frozzy
La mascota de Frozz vive en la esquina inferior derecha trabajando en su escritorio. Cuando toca una pausa se pone de pie, salta y avisa hasta que le das clic; abre el aviso a su lado, te acompaña y al terminar celebra con «¡Firme!». Se puede arrastrar a otra parte; clic derecho = menú. Se oculta en presentaciones y pantalla completa.
## Actualizaciones automáticas
La app revisa el último Release de este repositorio al arrancar (a los 45 s) y cada 6 horas.
Si hay una versión nueva pregunta *«¿Actualizar ahora?»*; al aceptar la descarga, verifica su huella SHA-256,
se reemplaza y se reinicia sola. También: clic derecho → *Buscar actualizaciones*.

### Publicar una actualización (solo el dueño del repo)
1. Hacer los cambios en `src/Program.cs`.
2. Subir el número en `AppInfo.Version` (p. ej. `1.1.0` → `1.2.0`).
3. `git commit` + `git push` a `main`. El mensaje del commit aparece como «Novedades» en el aviso.
4. GitHub Actions compila y publica el Release `v1.2.0`. Las apps instaladas lo ofrecen en su próxima revisión.

Si se hace push sin cambiar la versión, no se publica nada.

## Uso
- Ícono junto al reloj → **clic derecho**: próximos avisos, *Hacer ahora*, *Pausar* (30 min / 1 h / 2 h / hasta mañana), *Resumen de hoy*, *Ver instructivo*, *Mostrar a Frozzy*, *Configuración*, *Buscar actualizaciones*, *Salir*.
- El aviso sale abajo a la derecha **sin quitar el foco** de lo que estás escribiendo. Botones: *Empezar / Listo*, *En 5 min*, *Omitir*.
- Solo avisa en horario laboral (por defecto L–V 07:00–17:30, configurable).
- Si no usas el equipo por 5 min o bloqueas la sesión, cuenta como pausa y reinicia los contadores.
- No interrumpe presentaciones ni aplicaciones a pantalla completa.

## Datos
- Configuración: `%APPDATA%\PausasFrozz\config.txt`
- Historial (para SST): `%APPDATA%\PausasFrozz\historial.csv` → `fecha,hora,recordatorio,accion` (hecho / omitido / pospuesto)

## Desinstalar
*Configuración de Windows › Aplicaciones › Aplicaciones instaladas › Pausas Frozz › Desinstalar*. El historial se conserva en %APPDATA%\PausasFrozz.

## Compilar localmente
`powershell -ExecutionPolicy Bypass -File build.ps1` (usa el compilador C# incluido en Windows).
