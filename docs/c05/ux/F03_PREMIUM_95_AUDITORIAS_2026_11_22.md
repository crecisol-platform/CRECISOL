# CRECISOL · C05 UX F03 — Homologación premium de la cuenta autenticada

**Microtarea:** elevar las seis vistas autenticadas a una valoración editorial individual objetivo de **9.5/10 o superior**.  
**Rama aislada:** `feature/c05-ux-f03-premium-9p5-20261122`.  
**Base inmutable:** `42409d0bce6c70f4fb6f25679340063a43b61a44`, que conserva F01/F02 integradas en candidata F06.  
**Estado de esta acta antes de CI nueva:** implementación realizada; certificación final por emitirse exclusivamente tras pruebas y nueva inspección de capturas. No extrapolar notas 9.1–9.3 del PR #176 o 9.5 de F02 a estas seis pantallas.

## 1. Observación real de la base

Se revisó el ZIP original del [workflow F02 #45077790926](https://github.com/crecisol-platform/CRECISOL/actions/runs/45077790926), SHA `45c456385aca1752f50be76af5d8f52b1c04339c`, que contiene **30 pares PNG/JSON de F03 (6 × 320/390/768/1024/1440)**, con las seis vistas visibles. La comparación de GitHub entre el SHA probado y la cabeza candidata previa a F03 mostró que `frontend/src/`, `backend/` y `database/` tenían cero diferencias de contenido; los añadidos F01/F02 eran documentación/tests/workflows. Así se evita comparar imágenes de otro código productivo.

Hallazgos por área, antes de los cambios actuales:

| Vista | Hallazgo de experiencia | Remediación presente en esta rama |
| --- | --- | --- |
| Resumen | Dispositivos y política ocupan mucho espacio vertical, botones equivalentes y estados sin un ritmo unificado | Menor padding de dispositivos, jerarquía de bloques y tokens semánticos de sesión verificada; no se ocultan sesiones ni fechas |
| Perfil | Título y descripción de perfil repetidos; nombre y preferencias poco destacados | Textos contextuales sin repetición, nombre/avatares con tokens y legibilidad homogénea |
| Llaves | El dispositivo registrado y la acción de revocación compiten visualmente | Borde de identidad, botón con semántica de peligro y referencia truncada desde el modelo anterior |
| Seguridad | Cuatro acciones protegidas se perciben equivalentes y planas | Borde institucional y ritmo uniforme; modales, MFA, contraseñas y recuperaciones permanecen intactos |
| Historial | Etiquetas de estado se estrechan junto al tiempo en móvil | Estado fluye bajo la descripción a ≤480 px; sin mostrar códigos crudos ni IDs internos |
| Aprobaciones | Bandeja y permisos pueden sentirse densos, botón de consulta domina sin contexto | Tarjeta con jerarquía y aviso contextual legible; permiso, listado enmascarado, selección, motivo, MFA y confirmación no se modifican |

**Defecto transversal visible en móvil:** texto de descubrimiento de pestañas largo y navegación horizontal con poca señal de desplazamiento; se reemplaza por `Explora las seis áreas · Desliza o usa las flechas para ver más.` con pestañas de altura mínima 44px y subrayado de estado activo. Los seis enlaces siguen existiendo como navegación real con `aria-current`, foco y selectores robustos.

## 2. Alcance exacto y salvaguardas

Permitido únicamente:

- `frontend/src/app/pages/identity-portal.component.html/css`
- `frontend/src/app/pages/identity-workspace.component.html/css`
- `frontend/tests/c05-f03-account.spec.ts`
- `qa/c05_ux_f03_premium_audits.py`
- Esta acta y `.github/workflows/c05-ux-f03-premium-9p5.yml`

**Backend, MySQL, migraciones, contratos de identidad, parámetros de sesión, autorizaciones, cookies, códigos MFA, recuperación, privacidad, C01/C02, `dev`, `main`, Maestro Único y SOLQARYN fuera de alcance de escritura.** `data-view` es solo un selector visual, nunca un selector de privilegios.

Las nuevas pruebas de navegador verifican 30 combinaciones (6 vistas × cinco anchos), altura táctil mínima 44px, navegación de seis pestañas, ausencia de overflow y continuidad de acciones; una trigésima primera prueba protege decisión vacía, confirmación requerida, MFA, motivo y advertencia de membresía. Se mantienen íntegramente todos los tests históricos del archivo.

## 3. Barreras de aceptación

**A — funcionalidad y trazabilidad:** comparativa de tags HTML contra base, 65/65 IDs de inventario, seis vistas y ocho archivos exactos de la microtarea; PASS solo al ejecutarse el auditor.

**B — seguridad y privacidad:** sin cambios a backend/DB; conteos exactos de formularios/campos/modales/handlers y comprobación de marcadores críticos (MFA, membresía, revocación, código de respaldo, logout), cambios solo de contenido visual; PASS solo al ejecutarse.

**C — arquitectura, accesibilidad y estilo:** diseño F02 tokenizado, contrastes forzados, navegación con teclas/tacto, escalado de texto, estados de error/carga/vacío, evidencia 30 capturas exactas y regresión del inventario 65×5; PASS solo al ejecutarse. Las auditorías fuente no sustituyen Chromium ni un lector de pantalla humano.

**Integración Angular → .NET 10 → MySQL 8.4:** CI nueva en base de datos desechable; proteger negativas de permisos entre tenants, sesión, MFA, passkeys y aprobación. Nunca registrar una prueba no ejecutada como PASS.

**Calificación editorial:** revisar cada una de las seis nuevas capturas móvil y escritorio y flujos sensibles (especialmente aprobaciones, MFA, acciones de llave). Registrar 6 notas específicas, motivos y riesgos. El umbral es 9.5 **en cada vista**, no un promedio. Las puntuaciones antiguas 9.1–9.3 pertenecen al SHA de F03 original; las 65 notas de F02 tampoco sustituyen la evaluación F03.

## 4. Integración y cierre

Mantener esta microtarea en una rama hija de la candidata F06. PR #176 conserva su historia y una base F02 anterior; no fusionarlo sobre el trabajo nuevo sin conciliar diferencias. PR #172 sigue bloqueando la integración de la cadena a `dev` por cutover irreversible TOTP/recuperación persistente, respaldo SQL+llaves y dispositivos físicos. **Cerrar F03 en rama aislada no equivale a certificar producción o habilitar C06.**

## 5. Resultados CI y dictamen final

**PENDIENTES DE EVIDENCIA NUEVA.** La CI propia debe completarse y sus artefactos deben identificarse por SHA. La evaluación editorial IA se documentará por separado, sin asignar 9.5 anticipado.

### Corrección de causa raíz tras CI de Chromium

La primera CI del SHA `90e5daf17038ce04d890bc7e4a302d19e4909a86` detectó **6/65 fallos en un mismo contrato táctil**, solo a 320 px: el alto calculado de la pestaña resultó **41.59375 px** frente a 44 px exigidos. Las otras 59 pruebas pasaron; se conserva el fallo histórico. La variable `--cs-hit-target` y la escala efectiva no garantizaban 44px físicos. Se corrigió **el CSS productivo**, no el test: `min-height: max(44px, var(--cs-hit-target, 2.75rem))` en 320–720, 721–1039 y ≥1040 px. Se amplió la verificación de los seis enlaces a las **5 resoluciones, 30 casos**, más el control administrativo protegido (31 verificaciones premium). La CI posterior debe dar evidencia en su nuevo SHA antes de emitir puntuaciones editoriales.

### Causa raíz y reparación posterior (nuevo SHA obligatorio)

El segundo run [#45110997378](https://github.com/crecisol-platform/CRECISOL/actions/runs/45110997378) registró **71 PASS y 12 FAIL de 83** (los seis destinos a 320 y 390 px). La primera regla local de 44 px era insuficiente: el selector global antiguo `cs-identity-portal .account-nav--workspace.account-nav--workspace.account-nav--workspace a` en `frontend/src/styles.css` tiene especificidad más alta y aplica `min-height: 2.6rem`, medido en **41.59375 px**. Se mantiene intacto `styles.css` para no alterar F02/C01/C02. La solución en CSS de F03 usa `:host .account-workspace-rail` más triple clase de pestaña, que supera al global con mayor especificidad y exige altura física mínima de 44px. El test sigue exigiendo ≥43.5px de tolerancia. No calificar la F03 ni cerrar este gate hasta el nuevo SHA validado.

### Regresión F06 de microtexto (corregida en nuevo SHA)

El run [#45111320164](https://github.com/crecisol-platform/CRECISOL/actions/runs/45111320164) alcanzó 83/83 F03 PASS, 30/30 PNG/JSON F03, navegación F03 18 PASS, laboratorio F02 11 PASS, operadores F04 34 PASS y modales F05 61 PASS; **F06 173 PASS/1 FAIL**: el test histórico requiere literalmente `seis áreas` en minúsculas en el aviso móvil, y el nuevo texto lo capitalizaba al inicio. Se corrige el mensaje a `Explora las seis áreas · Desliza o usa las flechas para ver más.` y la prueba nueva F03 a la misma semántica sin distorsionar/relajar la prueba histórica F06. La CI debe repetirse en SHA posterior, sin extrapolar aprobación de las fases no ejecutadas.

### Contrato E2E real de llaves (registro de fallo y reparación)

La CI [#45112049475](https://github.com/crecisol-platform/CRECISOL/actions/runs/45112049475) aprobó el trabajo de frontend (411 Playwright, 65×5, F03 PNG 30/30, A/B/C), pero **falló E2E en la aserción 199** después de 198 comprobaciones reales PASS. El escenario de passkeys `qa/c05_passkey_browser_checks.mjs` exige `getByRole('heading', {name:'Llaves disponibles', exact:true})`, mientras la mejora había renombrado ese encabezado a `Llaves registradas`. No hubo fallo de permiso o firma; el test no localizó el encabezado por cambio de texto. Se restaura **el texto de producto original `Llaves disponibles`**, sin modificar la prueba E2E ni el backend, y se agrega una expectativa accesible explícita al test F03 en cinco tamaños para prevenir la regresión. Se repite toda la CI sobre nuevo SHA; no se convalida el E2E fallido ni se le asigna PASS retrospectivo.
