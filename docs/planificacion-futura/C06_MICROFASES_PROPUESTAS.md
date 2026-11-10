# C06 · Autorización fina, protección de datos y privacidad — microfases propuestas

> **BORRADOR DE PLANIFICACIÓN; NO HABILITADO PARA DESARROLLO NI CERTIFICACIÓN.** Esta guía deriva del índice `docs/PLAN_37_CAPITULOS.md` y de las políticas transversales de CRECISOL. Antes de activar C06, contrastar cada punto con el texto íntegro del capítulo en el Word canónico, su SHA registrado en `docs/MASTER_REFERENCE.md` y sus anexos; ajustar el número o alcance por PR trazable. Este archivo **no modifica el Documento Maestro Único**.

**Puerta de entrada:** C05 certificado, acta aceptada y CI de `dev` verde. Ninguna microfase de C06 puede codificarse durante C01 u otro capítulo anterior.

**Riesgo rector:** P0: pertenencia, rol, acción, branch y dueño verificados en backend.

## Plan secuencial de 11 microfases

| Código | Entregable acotado | Prueba o evidencia positiva esperada | Negativa que debe impedir cierre |
| --- | --- | --- | --- |
| C06-01 | Inventario de acciones por módulo | Catálogo canónico de permisos y ámbitos | No usar etiquetas UI como autorización |
| C06-02 | Modelo de roles y capacidades | Asignaciones por tenant y sucursal testeadas | No heredar rol de otra organización |
| C06-03 | Pertenencias y resolución de actor | Verificación real de identidad/membresía | No aceptar actor indicado por navegador |
| C06-04 | Políticas ABAC y propiedad | Evaluación por recurso y acción en backend | Denegar IDOR por ID ajeno |
| C06-05 | Reautenticación de operaciones sensibles | Step-up y finalidad justificada | No bypass vía petición directa API |
| C06-06 | SuperAdmin excepcional | MFA, motivo, duración y log separados | No acceso permanente a datos privados |
| C06-07 | Privacidad y minimización de DTO | Proyección estricta y retención definida | No serializar campos internos o PII extra |
| C06-08 | Aislamiento de listados y paginación | Filters servidor en todas las consultas | No count/enumeración entre tenants |
| C06-09 | Descargas, logs y exportaciones | Autorización en medios y auditoría | No fuga cross-tenant por URL firmada |
| C06-10 | Pruebas BOLA/IDOR de regresión | Matriz de actor/tenant/rol/branch ejecutada | Cero acceso horizontal o escalado |
| C06-11 | Auditorías integrales y acta C06 | A/B/C y aceptación con ausencia P0/P1 | No habilitar C07 sin puertas privadas cerradas |

## Contrato obligatorio para C06-XX

Para activar cada fila, validar dependencia de la microfase anterior, matriz de requisitos del capítulo y archivos/rutas afectados. Producir `docs/c06/C06_XX_ESPECIFICACION.md` al **iniciar** esa microfase (no fabricar actas futuras); rama `feature/c06-xx-*`; cambio pequeño en `frontend/`, `backend/` o `database/` según corresponda; pruebas positivas y negativas **realmente ejecutadas**, auditorías A (trazabilidad), B (seguridad, tenancy y privacidad), C (arquitectura, rendimiento y UX), diff, PR, CI del último SHA, merge a `dev`, CI posterior, acta y reversión. P0/P1 o cualquier control no ejecutado obligatorio implica **BLOQUEADO**, no PASS. No modificar SOLQARYN ni producción.

## Puerta de certificación del capítulo

Cerrar las 11 microfases **no** certifica C06. Exigir auditorías integrales A/B/C del capítulo, regresión cruzada, ausencia comprobada de P0/P1, aceptación expresa prevista en la gobernanza, acta final y evidencia GitHub del SHA integrado antes de habilitar C07. No confundir definición de prueba con prueba ejecutada; ninguna prestación del presente plan está implementada por el hecho de documentarla.\n
## Ampliación comercial COM-001..COM-014 — dependencia transversal (10-11-2026)

**Solo planificación; NO IMPLEMENTADO / NO CERTIFICADO.** Requisitos derivados de `docs/ANALISIS_IMPACTO_MODELO_COMERCIAL_2026-11-10.md` y de una actualización del Word maestro **todavía pendiente de incorporación y comprobación de huellas en GitHub**; esta nota no sustituye el Word ni habilita C06.

- **COM:** COM-008, COM-010, COM-012, COM-014.
- **Microfases a revisar sin renumerar:** C06-01, C06-02, C06-04, C06-05, C06-06, C06-07, C06-08, C06-10 y C06-11.
- **Aceptación futura:** Políticas para configurar ofertas (plataforma), consultar cuenta (propietario), solicitar baja (propietario), exportar sus propios datos y auditar administración.
- **Negativos:** Propietario no modifica precios globales ni contratos ajenos; SuperAdmin sin grant no inspecciona expedientes privados.
- **Pruebas definidas y NO ejecutadas:** COM-T07, COM-T08, COM-T10 y COM-T17.
- **Gate:** Word aprobado y verificable, capítulo precedente certificado, A/B/C, CI y acta propia. Sin datos productivos, despliegues ni pagos reales.
