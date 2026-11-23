# CRECISOL · C05 UX · Fase 01 — Inventario y línea base

**Fecha:** 19-11-2026. **Estado de F01:** PENDIENTE DE CERTIFICACIÓN VISUAL Y DE PRUEBAS.  
**Repositorio canónico:** `crecisol-platform/CRECISOL`, ID 1450414592. La antigua ruta `solqaryn-platform/CRECISOL` redirige a este mismo ID; NO es el proyecto `solqaryn/Solqaryn` y está prohibido escribir a este último. Ver `.agents/skills/crecisol-project-governance/SKILL.md`, `AGENTS.md` y `docs/MASTER_REFERENCE.md` en `dev`.

**Base congelada de análisis (antes del rediseño):** `f61a79d242288c686a88a7c3dd711f2be9a48c18`, PR #172, rama `feature/c05-totp-sha1-integrated-qa-20261118`. Rama documental/QA de F01: `feature/c05-ux-f01-inventario-20261119` creada desde ese SHA. PR #173 (`feature/c05-premium-ux-20261119`, SHA observado `4ead950c...`) contiene cambios de posteriores fases en DRAFT: **no usarlo para calcular una línea base previa a las mejoras, no fusionarlo ni modificarlo durante F01**.

**Autoridad documental:** Documento Maestro Único C05 y estado vigente `docs/ESTADO_VIGENTE_CAPITULOS.md` (5/37 certificaciones históricas en alcance local; C06 bloqueado); no se reinterpretan certificaciones anteriores como nota visual premium de 9/10. Se mantiene estrictamente el alcance código/documentos/QA; no VPS, producción, datos reales, correo real, pagos o migraciones persistentes.

## Entregables auditables

1. [Matriz de interfaces, variantes y estados](F01_MATRIZ_INTERFACES_BASE.csv): **65 entradas** (17 vistas, 18 estados, 3 complementos, **27 modales**). Incluye localización Angular, ruta, permisos/ámbito de prueba, operación, contrato y nota visual inicial.
2. [Matriz de contratos de transporte](F01_CONTRATOS_API_BASE.csv): **74 operaciones POST** declaradas en el diccionario de `OperationalIdentityModule.Operations` (43 del ámbito cuenta, 31 de plataforma), más tres rutas independientes de registro y una ruta GET privada: **78 entradas**. El mapeo de campos es del código real; no se inventan permisos a partir de etiquetas del frontend.
3. [Guía de capturas y criterios de evidencia](F01_CAPTURAS_Y_GATE.md): dispositivos, estados, seguridad, datos de prueba, condición de aceptación.
4. Test reproducible `frontend/tests/c05-f01-visual-baseline.spec.ts`: capturas automáticas ficticias y metadatos individuales. Workflow aislado `.github/workflows/c05-ux-f01-baseline.yml` para ejecutar Chromium y conservar resultados con SHA.
5. [Auditorías y acta de cierre](F01_ACTA_Y_AUDITORIAS.md): A funcional/inventario; B privacidad/autorización; C accesibilidad/rendimiento y evidencia visual. **No certificar F01** hasta cumplir su gate real.

## Inventario estructural cotejado con el código

### Angular: rutas y estados

- **Router** `frontend/src/app/app.routes.ts`: la única ruta de entrada C05 es `/cuenta` (lazy). `/experiencias`, `/demostraciones`, `/alta-empresas` y `/` pertenecen al marco público C01/C02, no nuevas pantallas privadas C05.
- **Vista dinámica** `IdentityPortalComponent.view`: admite exactamente `ingresar`, `registro`, `confirmar`, `recuperar`, `invitacion`, `acceso-existente`, `operadores`, `resumen`, `perfil`, `llaves`, `seguridad`, `administracion`; un parámetro no admitido retorna a `ingresar`.
- **Cuenta empresarial** `IdentityApiService.state`: `anonymous`, `challenge`, `authenticated`; su vista está condicionada por el estado, no solo por `?vista=`.
- **Operadores de plataforma** `PlatformIdentityApiService.state`: `anonymous`, `challenge`, `authenticated`; la consola separa cinco áreas: `casos`, `sesiones`, `perfil`, `seguridad`, `monitor`.
- **Modal compartido:** `frontend/src/app/ui/institutional-modal/institutional-modal.component.{ts,html,css}`. Son **27 instancias declaradas**: 15 cuenta autenticada, 1 recuperación de operador no autenticado, 3 casos, 2 sesiones de operador, 2 perfil de operador, 4 seguridad de operador. Incluyen formularios y confirmaciones sin formulario.
- **Formularios `<form>` existentes:** **47** distribuidos así: invitado 5, desafío MFA 1, recuperación 6, cuenta autenticada 15, términos 1, login/MFA/recuperación operador 6, casos 4, sesiones operador 1, perfil operador 2, seguridad operador 6. Estos son conteos de fuentes, no pruebas de que funcionen.
- **Servicios que deben inspeccionarse por estado:** `identity-api.service.ts`, `platform-identity-api.service.ts`, `browser-session-window.ts`, `totp-setup-qr.ts`, `identity-flow-views.component.ts`, `platform-operator-views.component.ts`, `identity-portal.component.ts`, `platform-identity-panel.component.ts` y `branding/public-brand.service.ts`.

### Casuística obligatoria de captura

**Entrada anónima:** login, carga, correo/contraseña inválidos, credenciales rechazadas, registro y confirmación, restablecimiento, denuncia, asistida por correo, asistida por dos operadores, invitación, membresía existente, WebAuthn no disponible y vinculación de navegador.

**Challenge MFA:** segundo factor vigente y respaldo, inscripción TOTP-SHA1 de 6 dígitos/30 s, QR/manual, error, desafío consumido, sesión/desafío caducados. No capturar secretos verdaderos.

**Cuenta autenticada:** resumen y sesiones (vacío/una/varias, cierre individual/todas, renovación), perfil/fotografía (sin/con imagen, edición/resultado), historial (vacío/abundante), llaves (sin/con registro, alta, baja, error WebAuthn), seguridad (password/MFA/correo/recuperación), aprobaciones (permitida/denegada) y consulta privada autorizada/denegada.

**Plataforma:** login/MFA/recuperación; casos (consulta, localización, evidencia independiente, decisión, demora 24 h, bloqueo, parte segura); sesiones (vacío/varias, rotar, revocar), perfil, cambio de correo/contraseña/factor, política de expiración (administrador `canManage` vs lectura) y monitor (vacío/con agregados). Separación de ámbito global y empresa obligatoria.

**Transversales en cada contexto aplicable:** initial, loading, success, error, empty, forbidden, expired (inactividad administrable, valor actual por defecto 30 minutos), viewport, teclado, scroll, zoom 200 %, forced-colors, reduced-motion, contraste, alertas, interacción y retorno del foco. Una variante no representable en el fixture debe marcarse **PENDIENTE**, no omitirse.

## Trazabilidad y permisos

La API operativa está vinculada a `backend/src/Crecisol.Api/Modules/Identity/OperationalIdentityModule.cs`: `MapPost` transforma las claves `platform/*` en `/api/v1/platform-identity/*` y el resto en `/api/v1/identity/*`. Los campos de la matriz vienen de `Operations`. `OperationalRegistrationProofModule` y `OperationalRegistrationCompletionModule` aportan las rutas de confirmación/completado; `PrivateResourceReadModule` publica la lectura privada GET.

**Las etiquetas de permisos de la matriz son hipótesis de prueba del recorrido, no certificación de autorización.** El backend sigue siendo la autoridad: host verificado, relación tenant/empresa/sucursal/recurso, rol, autenticación/MFA, CSRF, sesión válida, y denegación predeterminada. La composición C05 local restringe a loopback y `.example.test`; **no afirmar que está desplegada**. Validar positivos y negativos, IDOR/BOLA y cruce plataforma↔cuenta antes de cualquier aceptación funcional.

La UI captura mensajes de `IdentityPortalComponent.run`, `PlatformIdentityPanelComponent.run` y `cs-institutional-modal`. Deben compararse con los recibos reales: no equiparar un error neutral a existencia de cuenta, ni un recibo genérico a éxito de transacción. Revisar placeholders, etiquetas, contraste y habilitación/inhabilitación `busy()` y `restoring()`.

## Calificación inicial (heurística de muestras del propietario, no del SHA congelado)

**Origen:** capturas aportadas en este chat el 19-11-2026; no incluyen SHA, metadata de viewport normalizado, ni evidencia de integración con backend. Por ello las notas en el CSV son **orientativas** y **no son una certificación visual sobre `f61a79d...`**. La matriz se actualizó con **65/65 calificaciones iniciales provisionales** (promedio estimado **4,02/10**): **51 con confianza media** por referencia visual o familia y **14 con confianza baja** por inferencia/comportamiento aún no observado de manera independiente. La media histórica inicial de las 31 muestras originales era 4,29/10. Ninguna cifra supone calificación premium o certificación de permisos.

| Grupo de imágenes observado | Línea base orientativa /10 | Evidencia visual que motiva la nota |
| --- | ---: | --- |
| Operador: casos, sesiones, perfil, seguridad | 4.0–4.5 | Tarjeta excesivamente estrecha, jerarquía reiterada, exceso de botones uniformes, códigos en crudo |
| Operador: monitor | 4.0 | Indicadores sin jerarquía visual ni tratamiento de la densidad |
| Cuenta: resumen/sesiones, perfil, llaves, seguridad, aprobaciones | 4.0–4.5 | Espacios sin equilibrio, acciones planas repetidas, datos técnicos visibles |
| Historial de seguridad | 2.5–3.0 | Listas largas de eventos crudos ocupando la altura sin resumen ni agrupación |
| Condiciones y aceptación | 3.0 | Controles con estilo del navegador y texto de prueba con problemas de codificación |
| Modales: recuperación, revisión, MFA, WebAuthn, seguridad | 4.0–5.0 | Formularios repetitivos, scroll exterior, distancias verticales y estado/error poco diferenciados |
| Mensajes genéricos | 3.5 | El éxito puede no comunicar exactamente qué cambió o si hace falta reautenticar |

No medir visualmente solo con el número de formularios, el color azul ni el éxito de GitHub Actions. Una nota ≥9/10 será el umbral de **fases posteriores de mejora**; F01 debe establecer primero un inventario y una línea base reproducible, sin retocar el diseño.

## Riesgos y deuda de evidencia F01

- **Riesgo de auditabilidad acotado:** las fotos aportadas por el propietario no contienen SHA, pero el run F01 44619062318 produjo **212 PNG+212 JSON** sintéticos vinculados a base `f61...`, ver `F01_EVIDENCIA_VISUAL_Y_HALLAZGOS_2026_11_19.md`. La ampliación de estados y las capturas inferiores de modal requieren aún su propia validación.
- **Riesgo de sobreafirmación:** el workflow previo `c05-institutional-ui-audits.yml` del candidato #172 terminó SUCCESS (run 44601679085, 5 jobs), pero **no subió capturas**; su SUCCESS **no constituye** evaluación estética humana, ni certifica los 65 estados inventariados.
- **Cobertura manual obligatoria:** TOTP QR, WebAuthn con autenticador físico, expiración real, role mismatch, denied y casos de 24 h no deben considerarse satisfechos con señales de Angular alteradas sintéticamente.
- **Dependencias operativas:** Angular 22 exige Node `>=24.15.0 <25`, npm, Chromium y dev-server `127.0.0.1:4200`. El run GitHub 44619062318 sí compiló y ejecutó Playwright con éxito, generando capturas sintéticas. Es una ejecución en runner GitHub, no una ejecución local del propietario ni un backend/MySQL vivo.
- **Capturas de material sensible:** nunca registrar contraseñas, semillas, QR MFA reales, tokens, códigos de respaldo o PII en artefactos públicos; solo fixtures ficticios no reutilizables.

**Veredicto de inventario:** trazabilidad y primeras capturas verificadas; la **certificación F01 permanece sujeta al gate ampliado de PNG/JSON, auditorías A/B/C y acta con SHA final**. No avanzar a F02.

---

## Seguimiento transversal — 22-11-2026

Para reconciliar el inventario histórico con el producto visual evolucionado en la rama F06, consultar la [matriz cruzada de 65 filas](F01_MATRIZ_RECONCILIADA_F06_2026_11_22.csv) y la [adenda del acta F01](F01_ACTA_Y_AUDITORIAS.md). La matriz original permanece congelada. El SHA F06 confrontado es d539b75d13cde64c8ea3279d33ab35b6349c1adf, inventario #45032597168 (artefacto #14758472442) e integración #45032597178.

**Correcciones:** la interfaz 014 requiere condiciones realmente cargadas, y la 057 exige historial vacío real en /cuenta?vista=historial. La ruta 057 anterior es un antecedente incorrecto para esa evidencia, no prueba vigente. El nuevo verificador de 65 filas en qa/c05_ux_f01_reconciliation_audits.py exige estas distinciones.

El criterio actual es **>=9.5/10 en cada superficie**, jamás promedio; las notas iniciales y exploratorias no sustituyen el dictamen institucional. La certificación técnica histórica F01 se limita a inventario/línea base, sin equivaler a certificación comercial ni a fusión del PR #174.
