# C05 — Supervisión local persistente

Este procedimiento es exclusivamente local y de desarrollo. No conecta con producción, Cloudflare, SOLQARYN ni la base `crecisol_local` de C01/C02.

## Qué se crea

- Contenedor MySQL aislado: `crecisol-c05-mysql-persistent`.
- Volumen persistente aislado: `crecisol-c05-mysql-data`.
- Base de datos: `crecisol_c05`.
- Puerto MySQL: `127.0.0.1:3310`.
- Migraciones 001–017 para instalaciones nuevas tras el mantenimiento SHA1 aplicadas con control de versión y SHA-256.
- API C05 local: `http://127.0.0.1:5187`.
- Frontend C05 local: `http://127.0.0.1:4201/cuenta`.
- Buzón SMTP local para pruebas: `http://127.0.0.1:8025/`.

El entorno anterior continúa separado: C01/C02 usa `crecisol-local-mysql-20261113` en el puerto 3307, API 5184 y frontend 4200.

## Inicio y detención

Desde PowerShell:

```powershell
cd C:\Proyectos\FullStack\Crecisol
wsl.exe -d Ubuntu -- bash -lc "cd /mnt/c/Proyectos/FullStack/Crecisol && node qa/c05_local_persistent.mjs init"
wsl.exe -d Ubuntu -- bash -lc "cd /mnt/c/Proyectos/FullStack/Crecisol && node qa/c05_local_persistent.mjs start"
```

Mantén abierta la segunda terminal. `Ctrl+C` detiene API, frontend y SMTP, pero conserva el contenedor y el volumen C05.

Comandos de operación:

```powershell
wsl.exe -d Ubuntu -- bash -lc "cd /mnt/c/Proyectos/FullStack/Crecisol && node qa/c05_local_persistent.mjs status"
wsl.exe -d Ubuntu -- bash -lc "cd /mnt/c/Proyectos/FullStack/Crecisol && node qa/c05_local_persistent.mjs backup"
wsl.exe -d Ubuntu -- bash -lc "cd /mnt/c/Proyectos/FullStack/Crecisol && node qa/c05_local_persistent.mjs verify-backup"
wsl.exe -d Ubuntu -- bash -lc "cd /mnt/c/Proyectos/FullStack/Crecisol && node qa/c05_local_persistent.mjs stop"
```

`stop` detiene únicamente el MySQL de C05 y no borra sus datos. No ejecutes `docker volume prune` ni elimines `crecisol-c05-mysql-data` si necesitas conservar los registros.

## Acceso del propietario

Las credenciales generadas, semillas MFA, códigos de recuperación y la contraseña del buzón se guardan fuera del repositorio en:

```text
/home/javi/.crecisol/c05-local/access.md
/home/javi/.crecisol/c05-local/tenant-accounts.md
/home/javi/.crecisol/c05-local/tenant-accounts.json
```

Los archivos tienen permisos restrictivos y no deben copiarse a Git, tickets, capturas ni chats.

Perfiles ficticios disponibles:

- Operadores de identidad de plataforma: `identity.one@example.test`, `identity.two@example.test`, `identity.three@example.test`.
- Aprobador empresarial: `approver@example.test` en tenant A.
- Usuario empresarial: `member@example.test` en tenant A.
- Segundo tenant para aislamiento: `member-b@example.test` en tenant B; queda pendiente para pruebas de denegación.

Abre `/cuenta`, usa el enlace de personal autorizado para un operador o el acceso tenant para los usuarios empresariales. El primer acceso requiere MFA. Los correos de prueba se reciben únicamente en el buzón local autenticado; no se envía correo real.

## Aprovisionamiento y pruebas

El aprovisionamiento tenant, incluyendo la elevación local temporal del aprobador, se ejecuta offline y de forma idempotente:

```powershell
wsl.exe -d Ubuntu -- bash -lc "cd /mnt/c/Proyectos/FullStack/Crecisol && node qa/c05_local_persistent.mjs provision-tenants"
wsl.exe -d Ubuntu -- bash -lc "cd /mnt/c/Proyectos/FullStack/Crecisol && node qa/c05_local_persistent.mjs approve-member"
wsl.exe -d Ubuntu -- bash -lc "cd /mnt/c/Proyectos/FullStack/Crecisol && node qa/c05_local_persistent.mjs smoke"
```

La prueba smoke verifica contraseña, MFA, sesión y denegación de lectura privada entre tenants. Para crear datos ficticios, utiliza los formularios normales de la interfaz y el flujo de invitación/aprobación; no insertes datos manualmente en SQL.

Las sesiones, códigos de recuperación y permisos temporales caducan o pueden revocarse; la identidad y los registros de la base persisten. La recuperación usa la página de recuperación y los códigos protegidos de `tenant-accounts.md` o el flujo de recuperación de operador con otra identidad de plataforma.

## Copias y recuperación

`backup` exige una base C05 existente y encendida: no crea silenciosamente contenedores, volúmenes, cuentas ni llaves. Guarda un SQL comprimido y un manifiesto protegido (SHA-256 de archivo SQL, huella del historial 001–016 o 001–017, recuentos de operadores, cuentas, factores, sesiones, membresías y tenants, y SHA-256 de llaves/archivos externos existentes). `verify-backup` valida el manifiesto, comprueba que esas llaves todavía existen sin cambios y restaura SQL en un contenedor nuevo de red aislada, sin puerto publicado ni volumen persistente; compara historial y recuentos con el origen. Al salir elimina exclusivamente el contenedor temporal creado en esa ejecución.

**Importante:** el manifiesto contiene huellas, **no copia, protege ni restaura las llaves mismas**. Además de las copias SQL, el propietario debe conservar una copia independiente, cifrada y verificable de `runtime.env`, `mysql-root.password`, `access.md`, `mailbox.password` y **ambos archivos privados** de cuentas tenant (`tenant-accounts.json` y `tenant-accounts.md`) si existen; debe poder recuperarlos en un entorno C05 distinto. El manifiesto incluye huellas de ambos archivos presentes, pero nunca copia sus credenciales. Si aparece `access.md.sha1-pending`, `backup` y `verify-backup` se bloquean: primero hay que reconciliar el reaprovisionamiento offline; no se debe interpretar el archivo pendiente como recuperación completada. Es una barrera de aceptación antes del corte irreversible. Verificar los hashes de las llaves originales no demuestra que exista un respaldo recuperable de ellas. La base `crecisol_local` de C01/C02 nunca participa en este procedimiento.

Los respaldos históricos sin manifiesto no se convierten automáticamente en verificados: requieren revisión de restauración separada. No efectuar la migración 017 de un volumen anterior sin respaldos SQL **y llaves**, restauración comprobada y recuperación offline de operadores con la API detenida.

## Reversión segura

Detén el supervisor y conserva los archivos externos de secretos y backups. Si se decide retirar C05, elimina únicamente el contenedor `crecisol-c05-mysql-persistent` y el volumen `crecisol-c05-mysql-data` después de confirmar que el backup y su verificación están disponibles. Nunca elimines ni reinicialices `crecisol-local-mysql-20261113`, `crecisol-local-mysql-data-20261113` o la base `crecisol_local`.


## Dependencia de mantenimiento MFA SHA1 (PR #170)

Este PR de supervisión es complementario del **PR MFA SHA1 exclusivo #170**.
No desplegar este seguimiento ni usarlo sobre la base persistente anterior
hasta que el PR #170 supere cinco auditorías y las pruebas reales. Los factores
TOTP-SHA256 anteriores se invalidarán explícitamente: **las cuentas pueden
perder acceso**, por decisión del propietario; sus perfiles y tenants no se
borran automáticamente.

**No ejecutar `init` o `start` para completar automáticamente una base persistente anterior al corte**, incluso si el ledger está incompleto (por ejemplo, 001–015 o 001–016): el script rechaza cualquier base preexistente sin el historial 001–017 completo y verificable. Solo una base totalmente nueva puede inicializar 001–017 automáticamente. El script no aplica 017 sobre volúmenes existentes. Primero ejecutar `backup` y `verify-backup`, detener
API C05, validar un plan de reversión y seguir el manual
`docs/c05/C05_TOTP_SHA1_EXCLUSIVE_CUTOVER.md` del PR #170. A continuación
un operador local con autorización ejecuta la migración 017 una sola vez,
asienta su checksum en el ledger, recupera los tres operadores ficticios con
`qa/c05_sha1_local_operator_rekey.mjs` y reconfigura los usuarios empresariales
mediante recuperación legítima, nunca por acceso solo con contraseña.
La semilla nueva de cada cuenta se escanea una única vez; la sesión sigue
caducando y las acciones sensibles siguen requiriendo MFA.

### Defensas adicionales del supervisor persistente (mantenimiento C05 SHA1)

Antes de crear una tabla de migraciones, `init/start` comprueba las tablas
existentes en la base aislada: una base reutilizada con datos pero sin ledger
valido se rechaza sin intentar el corte. Una instalacion completamente vacia
puede ejecutar 001–017; una base existente solo puede continuar con las
17 versiones consecutivas y sus hashes SHA-256 validos. Versiones omitidas,
hashes alterados, corte parcial o version 017 ausente son NO-GO.

Antes de arrancar, consultar o detener el contenedor nombrado por el supervisor,
se verifica la imagen MySQL8.4 fijada, el volumen
`crecisol-c05-mysql-data` montado en `/var/lib/mysql`, la clave privada
montada en modo solo lectura y el puerto 3310 expuesto exclusivamente en
`127.0.0.1`. No se confia solo en el nombre del contenedor.

`qa/c05_sha1_persistent_guards.test.mjs` incluye 17 pruebas unitarias,
con casos positivos y negativos para ledgers parciales, datos preexistentes,
checksums incorrectos, imagen/volumen/puerto/claves impropios. Se ejecutaron
con Node.js v22.16.0: **17 PASS, 0 FAIL**, exclusivamente como pruebas
unitarias de los guardianes. En el entorno de esta revision no hay Docker
ni .NET10 disponibles: los ensayos completos del supervisor con MySQL 8.4
y el reenrolamiento fisico quedan **NO EJECUTADOS**, no certificados.

El entorno previo C01/C02, `crecisol_local` y sus puertos no se modifican.
Esta dependencia no implica haber migrado el volumen real del propietario ni
haber probado físicamente Google o Microsoft Authenticator.

### Guardián posterior al corte: cero operadores bloqueados al arrancar

En una base que ya registra C05-017, las órdenes `start` y las operaciones
de aprovisionamiento que levantan Kestrel se niegan a iniciar la API si la
política SHA1 no está activa, si los tres operadores de prueba no han salido
de `recovery_pending`, si sus sobres AES-GCM no tienen longitud SHA1 (48
bytes) o si faltan sus claves privadas de configuración de 32 caracteres
Base32 en `access.md`. Esto impide un arranque aparentemente correcto con
el ámbito de operadores todavía inhabilitado. No modifica identidades ni
credenciales y no reemplaza la comprobación real de acceso de cada operador.

Los comandos `status`, `backup` y `verify-backup` siguen habilitados
para diagnóstico y recuperación sin necesidad de activar un login.
