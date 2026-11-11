# C04 · Multitenancy, dominios y aprovisionamiento — microfases propuestas

**PRECONDICIÓN POSTERIOR (11-11-2026):** Una vez efectivas la aceptación y CI PRE/POSTMERGE del [acta integral C03](../c03/C03_CERTIFICACION_INTEGRAL_2026_11_11.md), este capítulo queda habilitado EXCLUSIVAMENTE para preparar C04-01, catálogo confiable de tenants; las 12 microfases siguen NO IMPLEMENTADAS y C04 NO CERTIFICADO. La advertencia histórica de borrador/no habilitado que sigue corresponde al estado anterior a dicha puerta. Consultar [registro de estado](../ESTADO_VIGENTE_CAPITULOS.md) antes de tocar código.

> **BORRADOR DE PLANIFICACIÓN; NO HABILITADO PARA DESARROLLO NI CERTIFICACIÓN.** Esta guía deriva del índice `docs/PLAN_37_CAPITULOS.md` y de las políticas transversales de CRECISOL. Antes de activar C04, contrastar cada punto con el texto íntegro del capítulo en el Word canónico, su SHA registrado en `docs/MASTER_REFERENCE.md` y sus anexos; ajustar el número o alcance por PR trazable. Este archivo **no modifica el Documento Maestro Único**.

**Puerta de entrada:** C03 certificado, acta aceptada y CI de `dev` verde. Ninguna microfase de C04 puede codificarse durante C01 u otro capítulo anterior.

**Riesgo rector:** P0: shared/dedicated; host verificado y router servidor.

## Plan secuencial de 12 microfases

| Código | Entregable acotado | Prueba o evidencia positiva esperada | Negativa que debe impedir cierre |
| --- | --- | --- | --- |
| C04-01 | Catálogo confiable de tenants | Estados y nombres sin exponer DSN | No crear tenant por Host arbitrario |
| C04-02 | Modelo relacional shared tenant-aware | FK e índices compuestos probados en MySQL 8.4 | Rechazar referencias cruzadas A a B |
| C04-03 | Contratos de almacenamiento dedicado | Puertos de ubicación tipados, decisión del servidor | No DSN desde URL, cookie o cabecera |
| C04-04 | Resolución de hosts verificados | Allowlist y ciclo de validación de dominio | No confiar en Forwarded no verificado |
| C04-05 | Contexto tenant por identidad | Contexto desde membresía/host autorizado | No usar tenant_id como permiso |
| C04-06 | Contexto de sucursal y recurso | Scopes por branch/owner en backend | No heredar permisos de otra sucursal |
| C04-07 | Aprovisionamiento idempotente | Creación sintética y recuperación ante reintento | No duplicar tenants por carreras |
| C04-08 | Suspensión, reactivación y borrado lógico | Transiciones seguras y auditables | Suspensión no deja acceso residual |
| C04-09 | Migraciones shared y dedicated | Ensayos de compatibilidad y rollback por ámbito | No migrar tenant equivocado |
| C04-10 | Aislamiento de caché y trabajos | Tests de keys, jobs, medios y exportaciones | No contaminar caché cruzada |
| C04-11 | Pruebas ofensivas multitenant | BOLA, IDOR, host, claims y DSN falsificados | Ninguna lectura/escritura cross-tenant |
| C04-12 | Auditorías integrales y acta C04 | A/B/C, matriz de fallos P0 y aceptación | No habilitar C05 con fugas abiertas |

## Contrato obligatorio para C04-XX

Para activar cada fila, validar dependencia de la microfase anterior, matriz de requisitos del capítulo y archivos/rutas afectados. Producir `docs/c04/C04_XX_ESPECIFICACION.md` al **iniciar** esa microfase (no fabricar actas futuras); rama `feature/c04-xx-*`; cambio pequeño en `frontend/`, `backend/` o `database/` según corresponda; pruebas positivas y negativas **realmente ejecutadas**, auditorías A (trazabilidad), B (seguridad, tenancy y privacidad), C (arquitectura, rendimiento y UX), diff, PR, CI del último SHA, merge a `dev`, CI posterior, acta y reversión. P0/P1 o cualquier control no ejecutado obligatorio implica **BLOQUEADO**, no PASS. No modificar SOLQARYN ni producción.

## Puerta de certificación del capítulo

Cerrar las 12 microfases **no** certifica C04. Exigir auditorías integrales A/B/C del capítulo, regresión cruzada, ausencia comprobada de P0/P1, aceptación expresa prevista en la gobernanza, acta final y evidencia GitHub del SHA integrado antes de habilitar C05. No confundir definición de prueba con prueba ejecutada; ninguna prestación del presente plan está implementada por el hecho de documentarla.\n
## Ampliación comercial COM-001..COM-014 — planificación condicionada (10-11-2026)

**Estado: propuesta documental / no implementada / no certificada.** La ampliación deriva del expediente `docs/ANALISIS_IMPACTO_MODELO_COMERCIAL_2026-11-10.md` y de la edición comercial preparada del único Documento Maestro; **esa edición DOCX/PDF ya fue incorporada a la rama del PR #73 con SHA y blob verificados; su aprobación normativa sigue pendiente de CI y fusión a dev**. Se conserva el número de microfases, la puerta de entrada secuencial y toda evidencia histórica. No se habilita código de C04.

- **Requisitos relacionados:** COM-001, COM-009, COM-010.
- **Microfases técnicas a revisar:** C04-01, C04-04, C04-07, C04-08, C04-11 y C04-12 (no equivalen a puertas normativas `MAE-C04-xx`).
- **Evidencia positiva futura exigida:** Registrar y verificar modalidad de dirección/ruta, subdominio o dominio propio; reservar y asignar host al tenant correcto; reconciliar suspensión y reactivación tras un pago efectivamente confirmado; mantener datos en retención sin borrado automático.
- **Negativos obligatorios:** Host, slug o DNS falsificados no elevan permisos; dos altas concurrentes no duplican tenant; suspensión no borra datos ni permite acceso residual; Tenant A no modifica tenant B.
- **Pruebas propuestas:** COM-T06, COM-T07, COM-T09 y COM-T17; `DEFINIDAS / NO EJECUTADAS`.
- **Gate:** revisar contradicciones y versión exacta del Word, políticas comerciales y jurídicas pendientes; pruebas reales + auditorías A/B/C + acta humana de C04 cuando el capítulo esté habilitado, sin cobros ni despliegues productivos en esta etapa.
