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

`backup` escribe el volcado comprimido fuera del repositorio. `verify-backup` restaura la copia más reciente en un contenedor temporal aislado y comprueba 16 migraciones para copias previas al corte o 17 posteriores. Las claves runtime permanecen en el directorio externo y no deben regenerarse al reiniciar. Una recuperación real debe restaurar conjuntamente el volcado y las claves en un entorno C05 temporal antes de reemplazar el volumen, siempre con una decisión explícita.

## Reversión segura

Detén el supervisor y conserva los archivos externos de secretos y backups. Si se decide retirar C05, elimina únicamente el contenedor `crecisol-c05-mysql-persistent` y el volumen `crecisol-c05-mysql-data` después de confirmar que el backup y su verificación están disponibles. Nunca elimines ni reinicialices `crecisol-local-mysql-20261113`, `crecisol-local-mysql-data-20261113` o la base `crecisol_local`.


## Dependencia de mantenimiento MFA SHA1 (PR #170)

Este PR de supervisión es complementario del **PR MFA SHA1 exclusivo #170**.
No desplegar este seguimiento ni usarlo sobre la base persistente anterior
hasta que el PR #170 supere cinco auditorías y las pruebas reales. Los factores
TOTP-SHA256 anteriores se invalidarán explícitamente: **las cuentas pueden
perder acceso**, por decisión del propietario; sus perfiles y tenants no se
borran automáticamente.

**No ejecutar `init` o `start` con una base de 16 migraciones esperando
actualización automática**: el script ahora se niega a aplicar 017 sobre
volúmenes existentes. Primero ejecutar `backup` y `verify-backup`, detener
API C05, validar un plan de reversión y seguir el manual
`docs/c05/C05_TOTP_SHA1_EXCLUSIVE_CUTOVER.md` del PR #170. A continuación
un operador local con autorización ejecuta la migración 017 una sola vez,
asienta su checksum en el ledger, recupera los tres operadores ficticios con
`qa/c05_sha1_local_operator_rekey.mjs` y reconfigura los usuarios empresariales
mediante recuperación legítima, nunca por acceso solo con contraseña.
La semilla nueva de cada cuenta se escanea una única vez; la sesión sigue
caducando y las acciones sensibles siguen requiriendo MFA.

El entorno previo C01/C02, `crecisol_local` y sus puertos no se modifican.
Esta dependencia no implica haber migrado el volumen real del propietario ni
haber probado físicamente Google o Microsoft Authenticator.
