# CRECISOL C05 UX F05 — Revalidación premium sobre F04 integrada

**Fecha:** 23-11-2026 (Honduras). **Rama aislada:** `feature/c05-ux-f05-premium-9p5-20261123`. **Base inmutable:** `1d8a8e552b6eb7749d81aafaaa1e5201163b6c4e` (F04 premium fusionada en candidata F06 por PR #182). **Estado:** EN VALIDACIÓN; no dar calificación 9.5/10 ni certificación de nuevo SHA sin evidencia posterior.

## Alcance quirúrgico
F05 abarca los cuatro patrones de `cs-institutional-modal` (simple, form, destructive, wizard) y sus 11 escenarios × cinco resoluciones. Se conserva el código funcional de F01/F02/F03/F04/F06 heredado. Las correcciones de esta ronda se limitan a:
- Evitar que un diálogo privilegiado se superponga a **cualquier otro diálogo modal nativo**, además de los instanciados por el componente de F05. No se intenta abrir ni aprobar operaciones mientras otro flujo de seguridad ocupa la top layer.
- Anunciar cambios de etapa de recuperación/MFA mediante región viva `aria-live="polite"` sin ocultar la lista progresiva ni modificar cuándo un paso queda autorizado por backend.
- Asegurar objetivos de cierre con ancho y alto nominal de 44 CSS px y mantener el contenido desplazable en 320/390/768/1024/1440 y texto ampliado.
- Expandir pruebas negativas automatizadas para el solapamiento externo, anuncios, tamaño táctil y regresiones de los once escenarios.

**Fuera de alcance:** autorización backend, sesiones (30 minutos de inactividad iniciales, 5–120 administrables), MFA TOTP SHA1, roles de operador, tenant/CSRF, PII, API, SQL, migraciones, repositorio SOLQARYN, `dev`, `main`, infraestructura y producción. No relajar o modificar las pruebas históricas fallidas para declarar PASS.

## Riesgos y puertas de validación
**A / Funcionalidad:** cuatro patrones, once escenarios, foco inicial/retorno, apertura/cierre y comandos reales; conservar validación y contratos. Compilar TypeScript y Angular22 bajo Node24, sin aumentar presupuestos CSS.  

**B / Seguridad/privacidad:** un único modal privilegiado incluso frente a un `dialog:modal` ajeno; escape bloqueado en transacción, sin doble envío, reseteo de formularios y limpieza de MFA, recuperación con dos revisores, sesión revocable, negativas BOLA/IDOR, CSRF, cookies y tenants en Angular→.NET10→MySQL8.4 desechable.  

**C / Experiencia y arquitectura:** 11×5 capturas PNG/JSON independientes, auditoría de hashes del SHA exacto, WCAG axe, teclado, 200 % de fuente, landscape, colores forzados, movimiento reducido, controles ≥44px, scroll y pie visibles; regresiones F02/F03/F04/F06 y catálogo 65×5. No inferir 9.5 a partir de CI.

**Gate de certificación:** tres auditorías A/B/C y ambas etapas de CI en SUCCESS sobre el mismo HEAD, sin P0/P1; capturas individuales revisadas para calificar cada uno de los 11 escenarios ≥9.5/10, separadamente de la aceptación humana. Las pruebas en teléfono/autenticadores físicos y el respaldo/restauración SQL+llaves del PR #172 continúan como barreras independientes para su integración institucional. Si no se dispone de capturas inspeccionables o del runtime necesario: estado PENDIENTE y constancia explícita, nunca PASS supuesto.

**Plan de reversión:** revertir exclusivamente el delta desde `1d8a8e55`; sin cambios de schema ni datos. No fusionar nuevamente #177/#178 ni escribir en `dev` o `main`.
