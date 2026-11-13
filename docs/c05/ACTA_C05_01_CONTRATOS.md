# CRECISOL — C05-01 · Acta institucional condicionada de contratos internos de identidad

**Elaboración:** 12 de noviembre de 2026, Honduras. **Repositorio autorizado:** crecisol-platform/CRECISOL, GitHub ID 1450414592. **Rama de integración:** dev. **Estado de esta acta hasta terminar sus puertas propias:** RATIFICACIÓN CONDICIONADA, **NO EFICAZ**. No certificar C05-01 antes de verificar CI PREMERGE/POSTMERGE documental, SHA final y comentario institucional permanente. C05-02 y C06 permanecen BLOQUEADOS hasta cierre del ámbito correspondiente.

## 1. Fuentes normativas y precedentes

- Documento Maestro Único Word docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx: SHA-256 ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c; PDF de lectura: SHA-256 0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77. Sin alterar ninguno.
- El Word contiene el capítulo 05 de identidad/autenticación/recuperación; extracción CI real 43364292640: 93 líneas aplanadas / 9 009 caracteres / SHA-256 bf7036849d6f1aad36a3a111b62121947aa12fae8c2a0551e63b0cc788d4a0aa. No confundir 8 puertas del proceso normativo con 11 microfases técnicas funcionales.
- C04-12 capítulo certificado exclusivamente en contratos y fixtures sintéticos, según PR #111 y comentario https://github.com/crecisol-platform/CRECISOL/pull/111#issuecomment-6843638186.
- Preparación C05-01 auditada mediante PR #112, CI 4/4 (PREMERGE y POSTMERGE) y comentario https://github.com/crecisol-platform/CRECISOL/pull/112#issuecomment-6843853811, dev de preparación 04c8ad23231a50829e5fbf9025f8b7d4789595b2.
- Fuente derivada de alcance: docs/c05/C05_01_ESPECIFICACION.md y docs/c05/IMPLEMENTACION_C05_01_CONTRATOS.md. El maestro Word/PDF prevalece; sus hashes no se reescriben.

## 2. Alcance realmente implementado

- **Domain:** backend/src/Crecisol.Domain/Identity/IdentityAccountModels.cs: cuenta global, metadatos de credenciales sin secretos, relación actor-tenant explícita con revisiones y fechas, perfiles Owner/Employee/Consumer SIN grants, operador global en realm separado, evidencia interna redactada y NO AUTORIZANTE.
- **Application:** backend/src/Crecisol.Application/Identity/ResolveCurrentAccountTenantLink.cs: correlación de actor/tenant por la cadena de confianza sintética de C04-05, consultas backend únicamente por actor verificado y tenant validado, rechazo de duplicados/ausencias/estados/expiración/cambios entre lecturas y segunda validación de contexto.
- **QA/.NET:** backend/tests/Crecisol.AccountIdentity.ContractsTests y qa/c05_01_identity_audits.py. No agrega rutas HTTP privadas, DI de auth, mecanismo JWT/MFA/recuperación, base MySQL de identidad, frontend de login ni servicios externos.
- **Cobertura de la microfase:** separación de identidad global vs pertenencia tenant, actor y perfil no autorizantes, metadatos sin secretos y vínculos independientes revocables. Una persona puede pertenecer a dos empresas con permisos distintos; ninguna membresía de A autoriza a B.
- **Limitación crucial:** snapshots y puertos C04 son sintéticos; sus constructores no validan tokens, firmas ni credenciales. No son autenticación persistente ni aislamiento end-to-end.

## 3. Auditorías A/B/C y pruebas reales

**Puerta técnica PR #113 — PREMERGE verificada (3/3 workflows aplicables SUCCESS), HEAD beab4638e960e746871d48143c4822a7dafdb549:**

| Ejecución | Evidencia | Resultado verificado |
| --- | --- | --- |
| C05-01 A/B/C integral | https://github.com/crecisol-platform/CRECISOL/actions/runs/43368796319 | completed/success; A Word y dependencias, B 80 mutaciones de barreras rechazadas, C SHA/run real; compilaciones Release sin advertencias, 100 aserciones .NET10 C05, todas las regresiones .NET C03/C04, Angular22/Chromium y nueve suites MySQL8.4 |
| C03-10 integral | https://github.com/crecisol-platform/CRECISOL/actions/runs/43368796356 | completed/success; regresión de arquitectura y módulo base |
| Foundation | https://github.com/crecisol-platform/CRECISOL/actions/runs/43368796352 | completed/success; integridad de repositorio/fuentes |

Workflows condicionales ajenos al scope marcados skipped **no cuentan como SUCCESS**.

**Puerta técnica POSTMERGE VERIFICADA (5/5 SUCCESS):** PR #113 fusionado EXCLUSIVAMENTE a `dev`, squash `d4a62cad50c66923dec2c871d844b3b578085896`. Las cinco ejecuciones aplicables finalizaron **`completed/success` sobre el mismo SHA**: [C05-01 A/B/C .NET/Angular/Chromium/MySQL #43369571344](https://github.com/crecisol-platform/CRECISOL/actions/runs/43369571344), [C03 integral #43369571202](https://github.com/crecisol-platform/CRECISOL/actions/runs/43369571202) , [C03-02 Domain #43369571147](https://github.com/crecisol-platform/CRECISOL/actions/runs/43369571147), [C03-03 Application #43369571151](https://github.com/crecisol-platform/CRECISOL/actions/runs/43369571151) y [Foundation #43369571127](https://github.com/crecisol-platform/CRECISOL/actions/runs/43369571127). Log final C05: **PASS A**, **PASS B (80 mutaciones negativas)**, **100 aserciones .NET C05**, nueve suites MySQL 8.4, Angular22 y Chromium reales, **PASS C** con `GITHUB_SHA=d4a62cad50c66923dec2c871d844b3b578085896` y `GITHUB_RUN_ID=43369571344`; el gate final de siete marcadores del mismo SHA/run fue `success`. Es evidencia sobre **contratos internos/fixtures**, no autenticación HTTP productiva.

**Auditoría A:** hashes maestros y texto C05, puerta C04/C05 de planificación, relación de actores/empresas, pruebas positivas y dos numeraciones distintas. **Auditoría B:** 80 mutaciones adversarias de guardas rechazadas, 100 aserciones ejecutadas de metadatos de credenciales, A/B tenancy, duplicados/revocación/expiración, reloj UTC y carreras, sin material privado en DTOs. No implica pentest HTTP productivo. **Auditoría C:** arquitectura Domain/Application separada, ninguna ruta ni persistencia nueva, .NET10 Release warnaserror + API y suites históricas, Angular22 Node24, Chromium y MySQL8.4 desechable, marcadores del mismo SHA/run en CI.

## 4. Errores históricos conservados y correcciones

Los fallos previos **no son PASS** y permanecen en GitHub:
- Runs 43368344568 y 43368344539: fuente de puerta C04 en auditor C05 referenciada incorrectamente y activación accidental del workflow documental de preparación al tocar un archivo de implementación. Corregido cotejando C05-01 preparación certificada y aislando el patrón de rutas de implementación.
- Run 43368472478: colisión C# CS0136 con argumento implícito del programa de pruebas. Corregida la variable de reflexión sin bajar severidad de compilación.
- Run 43368592121: test esperaba una excepción distinta ante una inconsistencia detectada por el contrato C04 de membresía ajena. Corregida la expectativa exacta del fallo cerrado; no se relajó el control.
- Nuevo HEAD beab4638e960e746871d48143c4822a7dafdb549 y run 43368796319 validaron de nuevo todas las puertas técnicas PREMERGE sin fallos vigentes. Ningún fallo histórico se reetiqueta.

## 5. Riesgos residuales fuera del ámbito y prohibiciones

**NO IMPLEMENTADO/NO CERTIFICADO PRODUCTIVAMENTE:** login real, hashes de contraseña Argon2id, passkeys/WebAuthn, TOTP/MFA, recovery token de un uso, sesiones/refresh/cookies, anti-CSRF, rate limit, verificación de correo, invitaciones, autorización privada SuperAdmin, fotos/medios, JWT/SSO, router físico Dedicated, workers/Redis, base de datos persistente de usuarios o pertenencias, backups de clientes, pagos, DNS, VPS/Cloudflare. Las amenazas preparatorias T03/T07/T09..T16/T18..T21/T23 sobre esos servicios **NO EJECUTADAS**. El control de scope C04-06 sigue exigiendo autorización backend de recurso y transacción real en integración posterior.

**Nada de producción, compras ni despliegue.** Mantener frontend/, database/, API pública, Word/PDF, main y solqaryn/Solqaryn intactos. Reversiones sujetas a análisis de dependencias, únicamente sobre dev.

## 6. Condiciones institucionales NO-GO

1. Puerta técnica POSTMERGE **cumplida** sobre `d4a62cad50c66923dec2c871d844b3b578085896` con 5/5 CI aplicables `completed/success`, sin fallos vigentes en el SHA y sin P0/P1 detectados en contratos internos/fixtures.
2. Preparar y auditar este documento en un PR independiente, verificar diff exacto y CI PREMERGE sobre su HEAD, con A/B/C + regresiones reales; no insertar texto inverificable de resultados futuros.
3. Fusionar documento EXCLUSIVAMENTE a dev con expected_head_sha; observar CI POSTMERGE documental del squash exacto, toda en success.
4. Publicar comentario institucional permanente en PR documental con ambos SHAs finales, SHAs y runs de las cuatro puertas técnicas/documentales PREMERGE y POSTMERGE, alcance, riesgos, fallo histórico y estado de main. **Antes de ello, C05-01 NO CERTIFICADA y C05-02 BLOQUEADA.**

**Si cumple todas las puertas, dictamen limitado:** C05-01 CERTIFICADA **SOLO EN CONTRATOS INTERNOS Y FIXTURES SINTÉTICOS**, 1/11 microfases C05, capítulo C05 NO CERTIFICADO, 4/37 capítulos previos completos y C05-02 habilitada SOLO PARA PREPARACIÓN. Ninguna integración productiva queda aprobada por esta acta.

**Rollback:** revertir los squash de implementación y acta de C05-01 únicamente en dev con revisión de dependencias; nunca editar SOLQARYN ni main.
