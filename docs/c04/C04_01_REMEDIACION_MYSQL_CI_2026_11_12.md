# CRECISOL — C04-01 · Incidencia de integración: arranque final MySQL 8.4

**Fecha:** 12 de noviembre de 2026, Honduras.  
**Repositorio:** `crecisol-platform/CRECISOL`, ID `1450414592`; destino `dev`.  
**PR técnico C04-01:** [#83](https://github.com/crecisol-platform/CRECISOL/pull/83), SHA `e7d9884b87a5f058a294af81573eec907614d65d`.  
**Estado:** REMEDIACIÓN DE CI EN CURSO, NO CERTIFICACIÓN DE MICROSERVICIO O TENANCY OPERATIVA.

## Incidente medido, sin atribuir éxito a un fallo

En el `push` POSTMERGE de PR #83 hubo cinco workflows SUCCESS (contratos C04-01, preparación C04-01, C03-02, C03-03 y Foundation), pero la [integral C03-10 run #43242306682](https://github.com/crecisol-platform/CRECISOL/actions/runs/43242306682) quedó **FAILURE**, por lo que se **suspendió el gate institucional de C04-01**.

El log confirma: compilación .NET10, Angular22 y Chromium exitosas; `database/tests/c03_07_outbox_mysql_smoke.sh` con **40 aserciones MySQL 8.4 y denegación DDL/DELETE** aprobadas, seguido por `database/tests/c01_04_api_mysql_smoke.sh` con error `ERROR 2002 (HY000): Can't connect to local MySQL server through socket '/var/run/mysqld/mysqld.sock' (2)` en su comienzo. **NO** se ha demostrado aún una fuga de tenants, sino una falla de conectividad del segundo ensayo MySQL. No se revoca retroactivamente la CI PREMERGE ni se atribuye éxito al run fallido.

## Causa probable, pruebas y corrección

El script original marcaba `ready` al primer `SELECT 1` del cliente del contenedor. El entrypoint oficial de MySQL inicializa por medio de un **servidor temporal** que luego detiene y sustituye por el final; un SQL temprano puede pasar y la siguiente consulta fallar durante la transición. **Hipótesis consistente con la secuencia temporal, no demostrada de forma exclusiva**. Otras causas de host/docker se descartan o confirmarán con CI nueva, no por intuición.

**Corrección acotada:** `database/tests/c01_04_api_mysql_smoke.sh` espera la marca `MySQL init process done. Ready for start up.` del contenedor oficial **fijado por digest SHA-256**, luego **dos sondeos SQL consecutivos** separados por un segundo; falla de manera cerrada después de límite temporal si el servidor final no está estable. No se modifica esquema, SQL funcional, grants, credenciales, endpoints, rutas o producción.

## Auditorías obligatorias del cambio

- **A — trazabilidad:** fuente real del log #43242306682, único archivo de smoke reparado y carta de compatibilidad con pruebas C03/C04.
- **B — seguridad:** mantener `docker run --network none`, pin de imagen `mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902`, directorio de socket efímero, grants `SELECT` solamente del lector, prohibición de `UPDATE`, contraseñas sintetizadas en ejecución y trap de cleanup. Ningún token productivo ni puertos publicados.
- **C — calidad:** `bash -n`, mutaciones negativas del gate de arranque, compilación real .NET10, smoke HTTP/MySQL 8.4 sintético, suite C03-10 integral y regresiones C04-01, con PREMERGE/POSTMERGE propios y SHA exacto. No certificar si cualquier check obligatorio es FAILURE, SKIPPED, QUEUED o no ejecutado.

## Condiciones y reversibilidad

Solo tras pruebas verdes de la corrección y la integral C03-10 sobre **su nuevo SHA de dev**, más la confirmación de que no existen P0/P1 en la superficie C04-01 implementada, se podrá firmar la [acta condicionada de contratos C04-01](C04_01_ACTA_CONTRATOS.md) mediante comentario verificable. La certificación será **solo del alcance interno Domain/Application**, no MySQL tenant-aware, host verificado, autorización productiva o catálogo operativo.

**Rollback:** revertir únicamente este PR de infraestructura de pruebas por SHA si se observa nueva regresión; nunca bajar cobertura, saltar MySQL ni alterar historial, Word/PDF, main, servicios productivos o `solqaryn/Solqaryn`.

**C04-01:** PENDIENTE de certificación por el fallo POSTMERGE histórico #43242306682; **C04-02 BLOQUEADO** hasta gate completo del PR correctivo.
