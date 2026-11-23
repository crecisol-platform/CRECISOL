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


## 4. Revisión editorial IA — once escenarios de F05 (23-11-2026)

**Producto inspeccionado:** `039a9286facf5b4f6e7a5ce129943e7d11e4b53e`. Las imágenes son del artefacto real [GitHub Actions #45208620429 / #14836425579](https://github.com/crecisol-platform/CRECISOL/actions/runs/45208620429/artifacts/14836425579). Se contrastaron capturas PNG independientes de 320px y 1440px para las once escenas, así como muestras de diálogos completos a 390/768px; las cinco resoluciones 320/390/768/1024/1440 tienen inspección automatizada de geometría, overflow, axe y teclado. La inspección editorial del asistente **no equivale a revisión humana de todos los estados, a recorrido completo con lector físico ni a transacción productiva**. La nota es únicamente juicio visual de IA en datos sintéticos.

| Escenario F05 | Nota editorial IA/10 | Justificación verificable en la captura |
| --- | ---: | --- |
| Recuperación de operador — solicitud | **9.5** | Jerarquía de tres pasos, ámbito excepcional y aviso de doble revisión legibles; entrada y CTA identificables en móvil |
| Recuperación de operador — doble control | **9.5** | Dos partes diferenciadas, contraseña y caso explícitos, sin afirmar aprobación local y con aviso de contenido desplazable |
| Reemplazo MFA — verificar | **9.5** | Contraseña y factor actual en orden verificable; etapa de configuración todavía bloqueada; foco visible |
| Reemplazo MFA — vincular | **9.5** | QR suficientemente protagonista, asistencia compatible TOTP SHA1, clave alternativa y pie de seguridad sin solaparse |
| Registro de llave de acceso | **9.6** | Formulario conciso con validación MFA y CTA principal inequívoco |
| Revocación de llave de acceso | **9.5** | Advertencia de consecuencias, referencia exacta seleccionada y confirmación diferenciada de consulta ordinaria |
| Cambio de contraseña | **9.6** | Campos en secuencia clara, MFA incluido y superficie compacta sin distraer de la acción |
| Revisión de membresía | **9.5** | Decisión irreversible contextualizada, solicitud seleccionada, motivo y MFA protegidos, scroll explícito para campos restantes |
| Invitación de cuenta | **9.6** | Alcance y caducidad visibles, correo/MFA con ruta primaria breve |
| Revocación de sesión de operador | **9.5** | Dispositivo y alcance de revocación identificables; advertencia y CTA disponibles mediante scroll interior anunciado |
| Renovación de sesión de plataforma | **9.6** | Propósito y efecto de rotación claros, acción controlada, sin sugerir ampliación de permisos |

**Resultado editorial IA:** 11/11 escenarios con mínimo individual **9.5/10**; no se usó promedio para aprobar. Todo recorte evitado gracias a scroll interior funcional/indicado en los escenarios largos; no se exige que todos los campos quepan en el primer pliegue de 320×720.

## 5. Evidencia técnica y límites — sin resultados anticipados

**Primera CI negativa conservada:** [#45207742136](https://github.com/crecisol-platform/CRECISOL/actions/runs/45207742136) ejecutó **60 PASS / 2 FAIL** por superposición de diálogos. El fallo era real: un escape de nueva línea convirtió en comentario el guard de exclusión. Se corrigió código producto y se reforzó auditoría B para prohibir guardas comentadas, sin alterar las expectativas Playwright.

**Run de producto corregido:** [#45208620429](https://github.com/crecisol-platform/CRECISOL/actions/runs/45208620429), SHA `039a9286facf5b4f6e7a5ce129943e7d11e4b53e`. Al registrar esta sección, la puerta de navegador terminó `completed/success`: **A/B/C PASS**, TS + Angular22 Node24, **62/62 F05 PASS**, **55 PNG/JSON exactos validados**, regresiones F02/F03/F04/F06 PASS y **30/30 Playwright inventario 65 superficies × 5 anchos**; el reporte de cobertura de 65 IDs PASS. La segunda puerta **Angular→.NET10→MySQL8.4** se mantiene explícitamente **EN EJECUCIÓN / SIN DICTAMEN** en este registro y **no debe darse por aprobada por inferencia**. La evidencia de capturas corresponde al SHA de producto indicado y no a un SHA documental posterior.

**Cierre técnico condicionado:** emitir dictamen de F05 únicamente al constatar **2/2 SUCCESS** en #45208620429 *y* **2/2 SUCCESS sobre el HEAD documental final** (nueva ejecución disparada por este acta); verificar 340 comprobaciones reales, 18 migraciones y 18 reversiones antes de aceptar la parte backend. Conservar el PR como borrador hasta verificar ambas puertas. La eventual fusión es **únicamente a la candidata F06**, no a `dev`/`main`. PR #172 y aceptación de autenticadores físicos y restauración SQL+llaves permanecen bloqueos separados. Ninguna de estas CI implica C06, hardware humano, producción, cliente real o despliegue.

**Evidencia de entrega:** RAR4 auténtico de 55 PNG originales (uno por escenario/resolución), manifiesto SHA-256 y lectura, cotejados contra el artefacto #14836425579. Archivo local `CRECISOL_F05_55_CAPTURAS_INDIVIDUALES_20261123.rar`, SHA-256 `a57246751a845f4ac2d61f285caf034ce30f33f40bff8aae18891387f2e60ea3`; el paquete está fuera del repositorio y no incorpora secretos productivos.
