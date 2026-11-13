# CRECISOL — C04-10 · Acta de control CONDICIONADA, NO EFICAZ

**Estado:** C04-10 EN DESARROLLO / NO CERTIFICADA. No es un acta de cierre. **Fecha:** 12-11-2026 (Honduras). **Repositorio:** `crecisol-platform/CRECISOL`, ID 1450414592. **Base verificada:** `dev` SHA `ba08137fc4ea1d39f21e63ce0cd9e3d309f585fb`.

## 1. Autoridad y dependencia previa

- C04-09 está certificada únicamente en su alcance interno/sintético mediante PR #101/#100 y [dictamen institucional](https://github.com/crecisol-platform/CRECISOL/pull/100#issuecomment-6840753129), con **20/20 CI aplicables SUCCESS**, cero pendientes/fallos de esos gates. No autoriza por sí sola cache/worker/Cloudflare privados productivos.
- Fuente canónica: `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, SHA-256 anotado `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c` en `docs/MASTER_REFERENCE.md`; capítulo 04, `docs/PLAN_37_CAPITULOS.md`, `docs/planificacion-futura/C04_MICROFASES_PROPUESTAS.md`, `docs/SEGURIDAD_MULTITENANT.md` y ADR-015.
- Alcance microfase: **claves de caché, jobs, medios, exportaciones**; bloquear contaminación de caché entre empresas.

## 2. Trabajos y puertas de pruebas

**PR técnico:** [#104](https://github.com/crecisol-platform/CRECISOL/pull/104), branch `feature/c04-10-artifact-isolation-20261112`, **DRAFT**, sin fusión. **PR documental:** [#103](https://github.com/crecisol-platform/CRECISOL/pull/103), branch `feature/c04-10-preparacion-20261112`, **DRAFT**.

**A — Requisitos y funcionalidad.** Claves estables solo en el mismo scope y revisión, separación entre cuatro propósitos, matriz de seguridad del maestro y ADR-015. Tests reales .NET10 y MySQL8.4 que demuestren la ausencia de alias entre tenants válidos A/B.

**B — Seguridad multitenant.** HMAC con secreto protegido por backend (secreto no versionado en producción); tenant/actor/branch/recurso/acción/permisos, revisiones/epochs de acceso, estado activo, expiración y placement Shared/Dedicated. Negativos de revocación a mitad de operación, lease fencing, job idempotente bajo carreras, owner cambiado, cache/media/export stale, operación/recibo/clave falsificada, DB role sin acceso directo. El fixture usa SQL logins desechables para simular ámbito protegido: **NO equivale a autenticación de usuarios reales**.

**C — Arquitectura, integridad y regresiones.** Domain sin frameworks, Application por puertos no conectados; DB fixture InnoDB temporal sin red ni puerto, referencias opacas en vez de medios o bytes, sin conexiones de producción, sin activación de Cloudflare/R2, VPS, DNS, pagos, Redis ni workers. Build API y suite de contratos Release `-warnaserror`, regresiones de C04-01..09 y C03-10 integral; CI por SHA exacto, seis referencias de prueba no equivalen a despliegue.

**Hallazgo adicional de diseño, pendiente de revalidación del SHA final:** dos vistas privadas de un mismo recurso (detalle/resumen o filtros aprobados distintos) no pueden compartir una entrada de caché solo por tener el mismo resource_id. El contrato agrega `ServerVariant` canónica en el fingerprint HMAC y negativos de variante/ruta arbitraria. La CI anterior a este cambio es evidencia HISTÓRICA, no una autorización para fusionar el HEAD nuevo.\n\n**Hallazgos que deben mantenerse abiertos hasta prueba real:** riesgo P0 de reutilizar la misma clave entre A/B aunque compartan `resource_id`, revocación de acceso después de encolar y antes de procesar, reactivar sin invalidar epoch, cambios de dueño/branch y ejecutor que confunde `operation_id` con permiso. Las nuevas restricciones de versión y revisión deben comprobarse, no certificarse por revisión visual.

## 2.1 Evidencia PREMERGE técnica disponible, NO cierre

El PR técnico #104 conserva HEAD `8a1e8fea89ca324a131cf6b3710e885f3745e74f`. El workflow específico [C04-10, run 43345315412](https://github.com/crecisol-platform/CRECISOL/actions/runs/43345315412) concluyó **completed/success** y registró en su log **145 aserciones .NET positivas y 119 negativas**, más **27 verificaciones MySQL 8.4 positivas y 43 negativas**, con 12 solicitudes concurrentes al mismo job (**1 claim y 11 BUSY, sin doble ejecución**). Auditorías A/B/C y seis mutaciones negativas estáticas de guardas aprobadas en el mismo SHA/run. Suite integrada `.NET 10 Release -warnaserror` y regresiones anteriores aprobadas.

**La regresión integral C03-10 todavía no se presume SUCCESS** al incorporar esta evidencia. Permanecen pendientes el total de workflows PREMERGE aplicables, la fusión técnica a `dev`, su CI POSTMERGE, las puertas del PR documental y el comentario institucional definitivo. Un workflow `skipped` condicional no cuenta como aprobado.

## 3. Condiciones de fusión y certificado

1. Confirmar SHA técnico final del PR #104 y que su diff contiene únicamente los contratos, las pruebas y los gates C04-10 sin conexión a producción.
2. Ejecutar A/B/C + suite .NET 10 + MySQL 8.4 + regresiones completas sobre **el mismo HEAD**, con cero P0/P1 y todos los workflows aplicables `completed/success`. Excluir `skipped` condicionales del conteo.
3. Solo después fusionar #104 a `dev`, registrar su SHA squash y exigir CI técnica POSTMERGE completa, con las auditorías del nuevo SHA.
4. Ajustar #103 a la evidencia real (SHA, conteos, run IDs), exigir CI documental PREMERGE, fusionar solo a `dev`, exigir CI documental POSTMERGE completa y verificar el SHA final.
5. Publicar comentario institucional permanente con todas las puertas y límites; solo entonces cambiar el registro `docs/ESTADO_VIGENTE_CAPITULOS.md` a **C04-10 CERTIFICADA EN ALCANCE INTERNO/SINTÉTICO**, 10/12; C04-11 habilitada únicamente para preparación. Hasta ese punto mantener C04-10 **PENDIENTE**, C04-11 **BLOQUEADA**, capítulo 04 NO CERTIFICADO (3/37 capítulos completos).

## 4. Límites y reversión

La certificación futura, si procede, NO abarcará cache/queues productivas, ejecución real de trabajos de clientes, Cloudflare/R2 y enlaces firmados, descargas, medios, exportaciones, autorización HTTP/middleware con MFA ni acceso a almacenamiento privado. Los artefactos se mantienen únicamente como contratos y fixtures desechables, sin blobs ni enlaces permanentes. La clave HMAC no concede permiso.

**NO modificar:** `main`, DOCX/PDF del Documento Maestro, Cloudflare, VPS, producción, cobros ni el repositorio `solqaryn/Solqaryn`. Reversión: cerrar PRs sin fusionar o revertir el squash exacto en `dev`; no hay DDL persistente del SaaS ni datos que migrar.
