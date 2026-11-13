# CRECISOL — C04-11 · Acta de auditoría ofensiva CONDICIONADA

**Nota de precedencia documental posterior:** esta acta registra el estado histórico PREMERGE y sus pendientes de entonces; no revoca el cierre posterior. La [ratificación institucional C04-11](C04_11_CERTIFICACION_INSTITUCIONAL.md) verifica las cuatro puertas originales **14/14 SUCCESS** y define las puertas adicionales de su PR. Hasta que su propia CI PREMERGE/POSTMERGE y comentario institucional permanente aprueben, la microfase sigue NO CERTIFICADA. Cuando ese dictamen final sea eficaz, prevalece sobre las expresiones históricas «en ejecución», «sin fusionar» o «pendiente» registradas más abajo, sin reescribir evidencias pasadas.


**Estado formal:** EN VALIDACIÓN / NO CERTIFICADA. **Fecha:** 12-11-2026 (Honduras). **Repo:** crecisol-platform/CRECISOL ID 1450414592, rama dev. **No es un acta de certificación todavía.**

## 1. Precondición

C04-10 está certificada exclusivamente en contratos y fixtures sintéticos por [dictamen #6841621470](https://github.com/crecisol-platform/CRECISOL/pull/105#issuecomment-6841621470), 28/28 workflows obligatorios sobre sus seis puertas; dev de entrada SHA a80fdc87fe15b13a22af268d007da88d2d270810. C04-11 requiere el vector oficial BOLA, IDOR, host, claims y DSN falsificados con prueba negativa absoluta de **ninguna lectura/escritura cross-tenant**. El Documento Maestro Único permanece sin cambios. [Especificación C04-11](C04_11_ESPECIFICACION.md).

## 2. Desarrollo, superficies y límites de alcance

[PR técnico #107](https://github.com/crecisol-platform/CRECISOL/pull/107) incorpora un harness de contratos .NET10, un runner ofensivo MySQL8.4 (Docker desechable sin red), QA independiente A/B/C y workflow de CI específico; no modifica código productivo ni frontend. [PR documental #106](https://github.com/crecisol-platform/CRECISOL/pull/106) incorpora la especificación y este acta hasta disponer de evidencia final.

- **A normativa/funcional:** matriz O11-01..10 contrastada con docs/PLAN_37_CAPITULOS.md, plan de microfases y contrato de seguridad; A/B legítimos pueden operar solo en su ámbito.
- **B seguridad multitenant:** resolución Host y catálogo protegido, actor/sesión/membresía, grants/branch/resource, return de placement de otro tenant, HMAC/fingerprint/epoch y receipt; tests reales de negación, ASI como SQL idempotente con roles sintéticos de mínimos privilegios, SELECT/UPDATE/DELETE/DDL denegados, FK compuestas A/B, inyección booleana, cambios de owner, revocación y 12-way job fence en regresión. Las **3 mutaciones de guardas** son verificaciones estáticas de auditoría y no deben contarse como pruebas de pentesting ejecutadas.
- **C arquitectura/performance/regresiones:** Domain/Application reales, API compilada .NET10 Release -warnaserror, contenedor MySQL 8.4 sin red/puertos, endpoints privados no conectados, fixtures C04-10 y C04-09 y puerta integral C03-10 independiente.

**Los ataques solo prueban los componentes implementados:** NO API privada JWT/MFA real, router de DSN productivo, Cloudflare R2/Stream, enlaces firmados, cola/worker real, Redis, exportaciones ni backups de cliente. Tales pruebas productivas no se ejecutaron y su ausencia no se registra como PASS. El resultado de MySQL sintético no certifica restricciones per-user en infraestructura aún inexistente.

## 3. Evidencia real del último SHA técnico (PREMERGE PARCIAL)

- **HEAD:** eb457652f7a8b0aa479a7e397989b19dc190fb25.
- **Workflow específico:** [43351613194](https://github.com/crecisol-platform/CRECISOL/actions/runs/43351613194) completed/success: auditorías A/B/C, builds Release, regresión .NET de C04-01..10 y pruebas MySQL.
- **Nueva batería ofensiva .NET10:** **29 positivas / 60 negativas** (89) sobre contratos Domain/Application originales, sin adición de endpoints ni adaptadores privados.
- **Nuevo MySQL ofensivo:** **14 positivas / 26 negativas** (40) en entorno desechable; B y A con el mismo resource_id; denegación de SQL directo, keys, media, export y jobs ajenos; las lecturas después de atacar confirman cero modificaciones del registro víctima.
- **Regresión MySQL C04-10:** 27 positivas/43 negativas (70); **12 solicitudes concurrentes reales = 1 CLAIMED y 11 BUSY**.
- **Regresión MySQL C04-09:** 38 positivas/33 negativas (71); recuperación con backup real de prueba, fencing y carreras de aprobación sin cruce.
- **Total comprobaciones observadas en estos cuatro subconjuntos de test:** 270 (sin duplicar como conteo de casos de pentesting productivo).
- La primera ejecución C04-11 del SHA c21a33e7133c1e2d747a12d4443e4d70837a4e8f, run [43351248766](https://github.com/crecisol-platform/CRECISOL/actions/runs/43351248766), quedó FAILURE: MySQL 8.4 rechazó la expresión booleana hostil con ERROR 1292 (conversión numérica) antes del SIGNAL SQL; el test solo contemplaba otros códigos. Se corrigió estrictamente en nuevo commit eb457652... para aceptar 1292 **únicamente para esa petición**, conservando el fallo histórico. Ningún dato cruzado se leyó o escribió en dicha ejecución, pero **esa ejecución no es PASS**.
- **Regresión integral C03-10 PREMERGE del SHA final: PENDIENTE** al redactar este acta. Ningún resultado pendiente/skipped cuenta como aprobado.

## 3.1 Fusión técnica permitida tras PREMERGE 3/3 SUCCESS

En el SHA técnico `eb457652f7a8b0aa479a7e397989b19dc190fb25`, **3/3 workflows aplicables completed/success, 0 pendientes/fallidos**: [C04-11 ofensiva 43351613194](https://github.com/crecisol-platform/CRECISOL/actions/runs/43351613194), [C03-10 integral 43351613276](https://github.com/crecisol-platform/CRECISOL/actions/runs/43351613276), [Foundation 43351613212](https://github.com/crecisol-platform/CRECISOL/actions/runs/43351613212). GitHub confirmó fusión `squash` del PR #107 **exclusivamente en dev**, commit técnico `3c9fbb7c2cf35584d379b94309c0c83fc4d9b32a`. No se modificaron componentes del producto, main ni bases productivas.

**Workflows técnicos POSTMERGE iniciados, NO presupuestos SUCCESS:** C04-11 [43352339752](https://github.com/crecisol-platform/CRECISOL/actions/runs/43352339752), C03-10 integral [43352339950](https://github.com/crecisol-platform/CRECISOL/actions/runs/43352339950) y Foundation [43352339794](https://github.com/crecisol-platform/CRECISOL/actions/runs/43352339794). Se exige completed/success 3/3 en ese SHA antes de fusionar el PR documental.

## 4. Puertas que deben quedar 100% SUCCESS ANTES de certificar

1. PREMERGE técnico completo de PR #107 sobre HEAD exacto, incluyendo C03-10 integral y Foundation, 0 fallos/pendientes aplicables; diff limitado a C04-11.
2. Fusión técnica por squash exclusivamente a dev, comprobar SHA y 100% POSTMERGE técnico (incluida regresión C03-10).
3. PREMERGE documental de PR #106 con workflow C04-11 disponible tras fusión técnica, 100% SUCCESS, diff documental exacto.
4. Fusión documental exclusivamente a dev, POSTMERGE documental 100% SUCCESS sobre SHA final.
5. Publicar dictamen institucional permanente con todas las ejecuciones, SHA y matriz A/B/C; sin omitir P0/P1 ni límites no productivos. Si el registro de estado sigue condicional, ratificar la eficacia en PR documental adicional con su CI.

**Hasta ese cierre:** C04-11 NO CERTIFICADA; C04-12 BLOQUEADA; capítulo 04 NO CERTIFICADO; capítulos completos 3/37. **Después, únicamente si TODO está aprobado:** C04-11 certificable SOLO en alcance ofensivo sintético (11/12), C04-12 habilitada SOLO PARA PREPARACIÓN.

## 5. Reversibilidad y prohibiciones

Se puede revertir el squash de PR técnico y documental sin migrar datos productivos; los Docker MySQL sintéticos se destruyen al salir. No modificar main, Word/PDF Maestro, VPS/DNS/Cloudflare productivos, pagos ni solqaryn/Solqaryn. La ausencia de endpoint privado real significa que la seguridad de un sistema productivo futuro sigue **NO PROBADA** en C04-11.
