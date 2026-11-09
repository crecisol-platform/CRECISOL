# C02 · Personas, experiencias y arquitectura de producto — microfases propuestas

> **PLAN OPERATIVO ACTIVADO SOLO PARA C02; CAPÍTULO NO CERTIFICADO.** C01 está certificado. C02-01 se contrastó con el Word canónico y su SHA, y se cerró técnicamente con PR #30/#31; C02-02 se cerró técnicamente con PR #32/#33, seis experiencias, 18 necesidades, 7 pasos y CI postmerge #42619923854 SUCCESS. C02-03 cerró técnicamente sitemap público: PR #34, cuatro rutas (tres informativas lazy), 8/8 Chromium y CI postmerge #42624454291 SUCCESS. C02-04 cerró técnicamente **solo diseño** de 4 contextos privados y 12 tareas (sin rutas operativas): PR #36, A/B/C, 18 negativos del contrato, 12/12 Chromium y CI postmerge #42630487933 SUCCESS. C02-05 cerró técnicamente estados universales de interacción: PR #38, cinco estados tipados, solo carga/error/offline públicos activos, 18 negativos, 19/19 Chromium y CI postmerge #42639836939 SUCCESS. C02-06 cerró técnicamente patrones UX de formulario/borrador y tabla de solo lectura en laboratorio Angular aislado, PR #40, 27/27 Chromium, 18 mutaciones y ocho CI postmerge SUCCESS incluido #42649577691; **sin pantallas de negocio operativas**. Avance **6/9**; C02-07 es la siguiente fase, todavía no iniciada. Las demás filas mantienen carácter de **propuestas sujetas al maestro y sus gates**; 8 puertas de proceso `MAE-C02-01..08` del Word no deben confundirse con 9 microfases técnicas `C02-01..09`. Ver `docs/c02/C02_01_ESPECIFICACION.md`. Este archivo **no modifica el Documento Maestro Único**.

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
