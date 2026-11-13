# CRECISOL — C04-11 · Ratificación institucional ofensiva (alcance sintético)

**Fecha:** 12 de noviembre de 2026, Honduras. **Repositorio autorizado:** crecisol-platform/CRECISOL, ID 1450414592. **Rama única de integración:** dev. **Microfase:** C04-11 — Pruebas ofensivas multitenant. **Estado del capítulo:** C04 completo NO CERTIFICADO. Esta ratificación queda CONDICIONADA a su propia CI PREMERGE, fusión exclusiva a dev, CI POSTMERGE y comentario institucional permanente con SHA final/runs reales; solo entonces se torna EFICAZ.

## 1. Autoridad y cadena previa

- Documento Maestro Único canónico: docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx; su referencia aprobada y huella SHA-256 se conservan en docs/MASTER_REFERENCE.md (ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c). No se ha editado ni reemplazado por otro maestro.
- Gobierno: .agents/skills/crecisol-project-governance/SKILL.md, AGENTS.md, docs/PLAN_37_CAPITULOS.md, docs/planificacion-futura/C04_MICROFASES_PROPUESTAS.md, docs/SEGURIDAD_MULTITENANT.md.
- Dependencia: [C04-10 certificada SOLO en contratos internos y fixtures sintéticos](https://github.com/crecisol-platform/CRECISOL/pull/105#issuecomment-6841621470); 28/28 workflows obligatorios, dev de entrada a80fdc87fe15b13a22af268d007da88d2d270810. No autoriza endpoints privados productivos.
- Requisito oficial C04-11: ataque de BOLA/IDOR, Host, claims y DSN falsificados; negativa obligatoria «Ninguna lectura/escritura cross-tenant». [Especificación C04-11](C04_11_ESPECIFICACION.md) y [acta condicional histórica](C04_11_ACTA_CONDICIONADA.md) conservadas para trazabilidad.

## 2. Implementación/ensayo real y aislamiento

PR técnico [#107](https://github.com/crecisol-platform/CRECISOL/pull/107), HEAD final eb457652f7a8b0aa479a7e397989b19dc190fb25, fusión squash SOLO en dev: **3c9fbb7c2cf35584d379b94309c0c83fc4d9b32a**. Cinco archivos de pruebas y QA C04-11 exclusivamente, sin modificar backend/src, frontend, schema productivo, worker, API privada, conexiones externas, configuración ni secretos:

- backend/tests/Crecisol.TenantOffensive.ContractsTests/Program.cs: harness .NET 10 que ejecuta código REAL de Domain/Application de C04-03, C04-04, C04-05, C04-06 y C04-10 con fakes sintéticos de lectores backend. Ataca dos empresas válidas A/B con el mismo resource_id, spoof de host/claims/DSN, usuario/branch/owner/grant/acción, revocación de membership/sesión, ubicación dedicada ajena, claves de caché y recibos de otros tenants, expiración/epoch/replay y cancelación. Un input del atacante nunca se transforma en autorización.
- database/tests/c04_11_offensive_sql.sh: MySQL8.4 InnoDB real en Docker DESECHABLE y SIN RED, roles locales de pruebas con solo EXECUTE, FK compuestas, operaciones y referencias separadas A/B; intentos hostiles de SELECT/UPDATE/DELETE/DDL directo y procedimientos de lectura/claim/ack de otro tenant. Confirma que los registros de B quedan intactos después de los intentos de A. En SQL booleano hostil MySQL 8.4 aplica rechazo de conversión ERROR 1292 antes del procedimiento.
- qa/c04_11_offensive_audits.py y .github/workflows/c04-11-offensive.yml: tres auditorías A/B/C diferenciadas, verificación de guardas/mutaciones estáticas, código limpio, CI por SHA/runs, Release .NET10 -warnaserror, nuevas pruebas y regresiones C04-01..10, MySQL 8.4 de C04-09/10, más C03-10 integral separada.
- PR documental [#106](https://github.com/crecisol-platform/CRECISOL/pull/106), HEAD f05bf7c120887ddcb8d4431259fcd62a080c27c4, squash SOLO dev **99156521cd08a9a43cccaa62d95eea12da791f28**, cambia tres documentos de C04-11 y registro vigente; conserva la condición anterior como hecho histórico.

**Prohibición de afirmaciones excesivas:** La ejecución NO prueba endpoints HTTP privados autenticados, middleware JWT real/issuer/audience, MFA/step-up, router físico de DSN, Cloudflare R2/Stream, enlaces de descarga, colas/workers productivos, Redis, caches CDN, backups ni exportaciones de clientes. Esas capacidades aún no existen en este alcance; toda validación productiva queda como FUTURA/NO EJECUTADA, no como SUCCESS. Ningún Docker de pruebas accedió a red externa, VPS o datos reales.

## 3. Auditorías A/B/C y resultados ofensivos verificables

- **Auditoría A — trazabilidad y positivos:** dos empresas legítimas aisladas pese a UUID de recurso coincidente; Host canónico y vínculo único verificado, lectura propia de cache, metadatos de medios y exportaciones, permiso de actor+branch+acción exacto y ubicación tomada desde catálogo backend.
- **Auditoría B — seguridad y negativos:** BOLA/IDOR cruzado, grants de B contra A, branch/owner de otra empresa, acción write con grant read, memberships/actor cambiados, Host con CRLF/wildcard/URL y expirado o duplicado, DSN de B inyectado, scope HMAC/ServerVariant/epoch/replay ajenos, SQL directo SELECT/UPDATE/DELETE/DDL, variables SQL de suplantación, FK cruzadas, suspensión/reactivación, ack/claim ajeno. Tres mutaciones estáticas de guardas detectables NO constituyen pentesting productivo.
- **Auditoría C — arquitectura y regresión:** puertos Domain/Application reales sin introducir adaptadores productivos; API pública compilada, MySQL8.4 aislado con PIN SHA de imagen, pruebas de permisos de runner, validación de limpiado de contenedor, C03-10 integral (.NET10/Angular22/Chromium/MySQL) y SHA exacto de CI.

**Cifras observadas en la ejecución específica sobre el HEAD técnico final, con repetición SUCCESS en ambos postmerge y en CI documental:**
- .NET 10 **29 positivas + 60 negativas = 89** de nuevos ataques.
- MySQL 8.4 ofensivo **14 positivas + 26 negativas = 40**, con 0 filas del tenant víctima alteradas.
- MySQL 8.4 C04-10 regresión **27 positivas + 43 negativas = 70**, y **12 reclamos concurrentes = 1 CLAIMED / 11 BUSY**.
- MySQL 8.4 C04-09 regresión **38 positivas + 33 negativas = 71**, con recuperación del respaldo sintético, fencing y carrera.
- **270 comprobaciones ejecutadas** sumando esos cuatro subconjuntos en un run específico; este total NO significa 270 ataques distintos ni incluye la regresión integral.
- Se preserva el run histórico [43351248766](https://github.com/crecisol-platform/CRECISOL/actions/runs/43351248766) FAILURE del SHA c21a33e7133c1e2d747a12d4443e4d70837a4e8f: el test esperaba solo SIGNAL/permiso y MySQL rechazó una expresión con ERROR 1292. El código quedó corregido en eb457652... para aceptar 1292 SOLO en ese vector; todas las pruebas posteriores fueron reales y completas, sin modificar la evidencia fallida original.
- Dentro del alcance de contratos y base efímera comprobados, **0 P0/P1 vigentes detectados**, pero NO se afirma riesgo cero general ni se certifica infraestructura futura.

## 4. Cuatro puertas originales de C04-11 — 14/14 SUCCESS

**Puerta 1 — técnica PREMERGE PR #107:** HEAD eb457652f7a8b0aa479a7e397989b19dc190fb25, **3/3 completed/success**: [C04-11 43351613194](https://github.com/crecisol-platform/CRECISOL/actions/runs/43351613194), [C03-10 integral 43351613276](https://github.com/crecisol-platform/CRECISOL/actions/runs/43351613276), [Foundation 43351613212](https://github.com/crecisol-platform/CRECISOL/actions/runs/43351613212).

**Puerta 2 — técnica POSTMERGE:** SHA 3c9fbb7c2cf35584d379b94309c0c83fc4d9b32a, **3/3 completed/success**: [C04-11 43352339752](https://github.com/crecisol-platform/CRECISOL/actions/runs/43352339752), [C03-10 integral 43352339950](https://github.com/crecisol-platform/CRECISOL/actions/runs/43352339950), [Foundation 43352339794](https://github.com/crecisol-platform/CRECISOL/actions/runs/43352339794).

**Puerta 3 — documental PREMERGE PR #106:** HEAD f05bf7c120887ddcb8d4431259fcd62a080c27c4, **4/4 completed/success**: [C04-11 43352398462](https://github.com/crecisol-platform/CRECISOL/actions/runs/43352398462), [C04-02 43352398382](https://github.com/crecisol-platform/CRECISOL/actions/runs/43352398382), [C03 institucional 43352398354](https://github.com/crecisol-platform/CRECISOL/actions/runs/43352398354), [Foundation 43352398300](https://github.com/crecisol-platform/CRECISOL/actions/runs/43352398300).

**Puerta 4 — documental POSTMERGE:** SHA 99156521cd08a9a43cccaa62d95eea12da791f28, **4/4 completed/success**: [C04-11 43352874535](https://github.com/crecisol-platform/CRECISOL/actions/runs/43352874535), [C04-02 43352874562](https://github.com/crecisol-platform/CRECISOL/actions/runs/43352874562), [C03 institucional 43352874586](https://github.com/crecisol-platform/CRECISOL/actions/runs/43352874586), [Foundation 43352874544](https://github.com/crecisol-platform/CRECISOL/actions/runs/43352874544).

**TOTAL ORIGINAL 3+3+4+4 = 14/14 workflows obligatorios completed/success**, 0 fallos vigentes y 0 pendientes en los cuatro SHAs verificados, sin dar por exitosa una ejecución anterior fallida.

## 5. Puerta de ratificación institucional adicional y dictamen condicional

Esta acta solo entrará en vigor después del PR de ratificación separado: **CI PREMERGE 100% SUCCESS sobre su HEAD exacto, fusión exclusivamente en dev, CI POSTMERGE 100% SUCCESS sobre el SHA institucional final, y comentario permanente con ambos conjuntos reales de run IDs**. Antes de concluir esos requisitos NO declarar C04-11 CERTIFICADA y NO habilitar C04-12. El acta condicional previa queda como documento histórico para no alterar resultados pasados.

**Solo tras la eficacia:** **C04-11 CERTIFICADA EXCLUSIVAMENTE EN PRUEBAS OFENSIVAS DE CONTRATOS INTERNOS Y FIXTURES SINTÉTICOS; C04 avanza a 11/12 microfases certificadas en su propio alcance, C04-12 HABILITADA ÚNICAMENTE PARA PREPARACIÓN. C04 COMPLETO NO CERTIFICADO; capítulos completos C01–C03 = 3/37.**

Reversión del código: revertir exclusivamente el squash PR #107 3c9fbb7c2cf35584d379b94309c0c83fc4d9b32a; documentación de PR #106 99156521cd08a9a43cccaa62d95eea12da791f28 y futura ratificación por su SHA final si fuera preciso. Ningún schema persistente del SaaS o dato productivo se migra. main, Word/PDF canónicos, VPS, DNS, Cloudflare, cobros y solqaryn/Solqaryn se mantienen intactos.
