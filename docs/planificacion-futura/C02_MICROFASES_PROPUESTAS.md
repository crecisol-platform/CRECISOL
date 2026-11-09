# C02 · Personas, experiencias y arquitectura de producto — microfases propuestas

> **ESTADO VIGENTE: C02 CERTIFICADO POR ACEPTACIÓN EXPRESA «APROBADO», 9/9 MICROFASES TÉCNICAS CERRADAS** (9 de noviembre de 2026), sujeto al control PREMERGE y POSTMERGE de la formalización. Aprobación posterior a triple auditoría A/B/C, 58/58 Chromium C02, regresiones C01, MySQL 8.4/.NET10/Angular22 reales de publicación pública y **12/12 CI POSTMERGE SUCCESS** sobre el acta técnica SHA `d8ba77dd65f87325ca8dbf2f38455943af2f8d8e`. [Acta técnica y certificación humana](../c02/C02_09_AUDITORIA_Y_ACTA.md) y [estado oficial](../C02_PREPARACION.md). C03 podrá planificarse tras verificar la CI de la formalización, pero **NO INICIADO**; no hay producción, pagos ni auth privada certificada. Las 8 puertas normativas `MAE-C02-01..08` del Word **no** equivalen a las 9 microfases técnicas `C02-01..09`. **Histórico: antes de C02-09 (8/9), el capítulo no estaba certificado y C03 seguía bloqueado**; ese estado fue sustituido por el cierre técnico y la aceptación. Este plan no sustituye al Documento Maestro ni certifica fases futuras.

**Puerta de entrada:** C01 certificado, acta aceptada y CI de `dev` verde. Ninguna microfase de C02 puede codificarse durante C01 u otro capítulo anterior.

**Riesgo rector:** Roles, flujos y accesibilidad; nunca conceder autorizaciones desde UI.

## Plan secuencial de 9 microfases

| Código | Entregable acotado | Prueba o evidencia positiva esperada | Negativa que debe impedir cierre |
| --- | --- | --- | --- |
| C02-01 | Inventario de personas y contextos | Perfiles plataforma, negocio, empleado y consumidor rastreables | No fusionar perfiles ni asumir permisos implícitos |
| C02-02 | Mapa de necesidades por experiencia | Objetivos y tareas por persona con criterios observables | No prometer módulos que otros capítulos aún no implementan |
| C02-03 | Arquitectura de información pública | Sitemap y rutas públicas validadas | No mostrar rutas privadas ni datos tenant |
| C02-04 | Arquitectura de navegación privada propuesta | Mapa de tareas condicionado al RBAC backend futuro | Un guard Angular nunca autoriza recursos |
| C02-05 | Estados de interacción universales | Loading, vacío, error, offline y confirmación definidos | No reutilizar datos de otro contexto al fallar |
| C02-06 | Patrones de formularios y tablas UX | Flujos de teclado, errores y edición segura prototipados | No perder cambios silenciosamente ni ejecutar HTML |
| C02-07 | Diseño móvil y accesibilidad basal | Recorridos móvil y escritorio con revisión WCAG | No ocultar funciones esenciales por viewport |
| C02-08 | Pruebas de recorridos y contratos de experiencia | Tests positivos y negativos de rutas y estados | No publicar módulos de negocio futuros |
| C02-09 | Auditorías integrales y acta C02 | Trazabilidad A, seguridad B, UX/arquitectura C, aceptación | No abrir C03 antes de capítulo C02 certificado |

## Contrato obligatorio para C02-XX

Para activar cada fila, validar dependencia de la microfase anterior, matriz de requisitos del capítulo y archivos/rutas afectados. Producir `docs/c02/C02_XX_ESPECIFICACION.md` al **iniciar** esa microfase (no fabricar actas futuras); rama `feature/c02-xx-*`; cambio pequeño en `frontend/`, `backend/` o `database/` según corresponda; pruebas positivas y negativas **realmente ejecutadas**, auditorías A (trazabilidad), B (seguridad, tenancy y privacidad), C (arquitectura, rendimiento y UX), diff, PR, CI del último SHA, merge a `dev`, CI posterior, acta y reversión. P0/P1 o cualquier control no ejecutado obligatorio implica **BLOQUEADO**, no PASS. No modificar SOLQARYN ni producción.

## Puerta de certificación del capítulo

Cerrar las 9 microfases **no** certifica C02. Exigir auditorías integrales A/B/C del capítulo, regresión cruzada, ausencia comprobada de P0/P1, aceptación expresa prevista en la gobernanza, acta final y evidencia GitHub del SHA integrado antes de habilitar C03. No confundir definición de prueba con prueba ejecutada; ninguna prestación del presente plan está implementada por el hecho de documentarla.
