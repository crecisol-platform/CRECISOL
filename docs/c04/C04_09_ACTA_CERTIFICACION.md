# CRECISOL — C04-09 · Acta institucional CONDICIONADA

**Fecha:** 2026-11-12 (Honduras). **Estado:** NO EFICAZ; C04-09 NO CERTIFICADA. **Repositorio:** `crecisol-platform/CRECISOL` (ID 1450414592), rama autorizada `dev`. **Alcance máximo posible al certificar:** contratos internos Domain/Application NO CONECTADOS y ensayos MySQL 8.4 desechables de esquema SHARED global y DEDICATED por tenant/slot. **NO** es un ejecutor de migraciones productivas.

## 1. Puerta de entrada C04-08

C04-08 fue certificada solo en alcance interno/sintético en [PR #98, comentario institucional #6838736796](https://github.com/crecisol-platform/CRECISOL/pull/98#issuecomment-6838736796), `dev` SHA `136c8697d662ed6f8b4213f95370312cc6654a4e`, tras 20/20 workflows aplicables SUCCESS. Ello permitió PREPARAR C04-09, no certificarla ni avanzar a C04-10.

Fuente canónica: `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, huella SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`, `docs/MASTER_REFERENCE.md` y matriz C04-09 «Migraciones shared y dedicated», positivo compatibilidad y rollback por ámbito, negativo prohibición de migrar tenant equivocado.

## 2. Expediente técnico y hallazgos

**PR preparatorio #100:** `docs/c04/C04_09_ESPECIFICACION.md` y `docs/c04/C04_09_AUDITORIAS_EXTRAORDINARIAS.md`. **PR técnico #101:** rama `feature/c04-09-migration-contracts-20261112`.

- **D (extraordinaria):** se corrigió riesgo P0 de pensar que SHARED DDL es un cambio de un tenant; SHARED tiene autorización de esquema global sin `TenantId`; DEDICATED usa tenant+slot exclusivo+revisión. SQL valida tenant B existente, slot B existente y una reparenting A→B sin colisión PK; ownership de slot única e inmutable; ejecutor del fixture no puede emitir DDL directo.
- **E (extraordinaria):** se introdujeron reserva transaccional de ledger, identidad de versión/checksum, un único scope inflight, contador de fencing, un orden de locks determinístico `approval → target → gate → ledger`, reevaluación de decisión revocada, detección de drift físico antes de replay, y estado `failed` hasta restauración real. Se prohíbe afirmar rollback transaccional de DDL de MySQL.
- **F (extraordinaria):** se incrementaron las pruebas unitarias .NET, dos suites MySQL 8.4 reales en aislamiento `--network none`, regresiones de microfases anteriores y una cadena de evidencia de SHA/run obligatoria; no se permite omitir fallos.

**Evidencia HISTÓRICA PARCIAL, NO FINAL:** en el PR técnico un SHA anterior `ba51775bcabbb88d505281bc2ff969dc62a7ccf4` tuvo una ejecución con seis auditorías A/B/C/D/E/F SUCCESS, 64 aserciones .NET10 y dos suites MySQL8.4: 15 positivas + 3 negativas en compatibilidad y 36 positivas + 26 negativas en pruebas endurecidas, entre ellas doce solicitantes (1 ganador, 11 rechazos) y restauración desechable con hash. **Esta evidencia NO permite certificar el HEAD posterior**, porque luego se agregaron correcciones de lock order, checks de drift, CAS de revisión global Shared y nuevas pruebas negativas. Las ejecuciones históricas con fallos de preparación permanecen inalteradas.

## 3. Seis auditorías finales independientes obligatorias

| Auditoría | Requisito para dictamen final |
| --- | --- |
| A | Fuente Word vigente, alcance y trazabilidad C04-09 |
| B | Seguridad base, checksum/fingerprint, revisiones, autorizaciones negativas |
| C | Domain/Application/Database y CI sin adaptador real ni DSN |
| D | Protección multitenant: A/B válidos, slot ajeno, Shared global, permisos mínimos |
| E | 12-way concurrency, fencing, deadlocks, CAS, replay, DDL parcial, backup+restore, drift físico |
| F | Regresiones .NET 10 reales + MySQL 8.4, mismo SHA/run, auditorías completas y scope del diff |

**No sustituir D/E/F por A/B/C ni afirmar PASS por mera existencia de código.** Verificar en logs reales todos los casos obligatorios sobre el SHA final.

## 4. Condiciones de eficacia, NO-GO y prohibiciones

1. **PREMERGE técnico:** CI completa con TODOS los workflows aplicables `completed/success`, seis auditorías y negativos reales, compilación .NET10 Release `-warnaserror`, dos suites MySQL8.4 desechables. Cualquier `queued`, `in_progress`, fallo, error o P0/P1 sin resolver implica **NO_GO**.
2. **Integración técnica:** revisar SHA/diff y fusionar PR #101 solo a `dev` después de cumplir puerta 1; verificar toda CI POSTMERGE técnica sobre commit exacto. Los `skipped` condicionales nunca son PASS.
3. **PREMERGE documental:** PR #100, acta, auditorías y estado institucional deben superar TODA CI aplicable en HEAD documental final.
4. **Integración documental:** fusionar PR #100 solo a `dev` tras puerta 3 y verificar CI documental POSTMERGE del SHA final.
5. **Cierre institucional:** comentario PERMANENTE en PR #100 con los cuatro conjuntos de runs, SHA técnicos/documentales, auditorías A/B/C/D/E/F, pruebas/conteos reales, alcance y cero fallos vigentes. Sin comentario o cualquiera de las puertas anteriores, el acta conserva estado NO EFICAZ.

**DICTAMEN CONDICIONAL:** Solo tras todas las puertas, C04-09 podrá declararse certificada **EN SU ALCANCE INTERNO Y SINTÉTICO**, avance capítulo 04 a 9/12, habilitando C04-10 solo para preparación. Hasta entonces C04-09 PENDIENTE, C04-10 BLOQUEADA, capítulo 04 NO CERTIFICADO, capítulos completos C01–C03 = 3/37.

**Límites P0/P1 fuera de alcance:** no existe backoffice administrativo autenticado/MFA real, aprobación persistente de migraciones, DSN/router protegido de producción, ejecución real de DDL en bases de clientes, backups de clientes, cambio de estructura productiva, integración de CI con despliegue, pagos ni acceso privado. No se confunden ensayos sintéticos con cobertura productiva; cualquier ruta real continúa cerrada. Prohibido modificar `main`, Word/PDF, producción, servicios externos o `solqaryn/Solqaryn`.

**Reversión:** antes de fusionar, borrar/revisar ramas/PR sin afectar dev; después, revertir exclusivamente el SHA técnico/documental de C04-09, que no tienen migraciones productivas. Cada DDL de prueba vive únicamente en contenedores efímeros eliminados al terminar.
