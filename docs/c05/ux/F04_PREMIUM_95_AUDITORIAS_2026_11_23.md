# CRECISOL · C05 UX F04 — Elevación premium de consola de operadores

**Fecha:** 23-11-2026 (Honduras).  
**Rama:** feature/c05-ux-f04-premium-9p5-20261123, basada en `31ff32d348606e77d311b6250a0e341eb052485a` (F01/F02/F03 integradas en candidata F06).  
**Estado actual:** certificación técnica y dictamen editorial IA aprobados para producto SHA `595a2f9892706a24f046cd3958699d81147ee961` (detalle en sección 5); la integración Git de esta adenda se verifica por separado. No constituye aprobación humana del propietario ni de producción.

## 1. Diagnóstico real y correcciones

El PR histórico #177 sigue DRAFT en una rama de base F03 antigua. Su código productivo fue incorporado a la línea candidata mediante la cadena F05/F06. No fusionarlo nuevamente. Históricamente las notas editoriales IA eran Casos 9.1, Sesiones 9.2, Perfil 9.1, Seguridad 9.0, Monitor 9.2. Se descargaron 25 imágenes de F04 del workflow #44680701140 y se revisaron como punto de partida.

| Área | Corrección aplicada en la rama actual | Controles inalterables |
| --- | --- | --- |
| Casos | Consulta exacta visualmente primaria, resultado separado, revisión y emisión protegidas diferenciadas, nota de seguridad visible | Sin bandeja global inventada; decisión vacía, doble declaración, evidencia SHA-256, motivo y MFA |
| Sesiones | Dispositivo con identidad visual y referencia corta; revocación de peligro distinta de renovación ordinaria | Operador actual únicamente, sesión seleccionada por ID en backend, revocación total mediante MFA |
| Perfil | Identidad institucional y detalles con mejor ritmo; edición y fotografía como tareas distintas | Perfil no cambia roles ni pertenencias; fotografía validada y sanitizada |
| Seguridad | Credenciales agrupadas, política central de inactividad separada de operaciones privilegiadas | 30 minutos iniciales, valor administrable 5–120, revisión de versión, MFA y permisos backend |
| Monitor | KPI principal destacado y señales agregadas por gravedad; lista con prioridades visuales | Solo sumas y allowlist semántico; no IP, correo, UUID, empresa o datos de tenant |

Se conserva la cabecera, navegación y avisos institucionales. Las etiquetas y acciones existentes no se renombran para no romper contratos E2E. El CSS es específico de las cinco áreas y no oculta formularios ni reduce tamaño de letra como técnica de ajuste.

## 2. Alcance, dependencias y riesgos

Exactamente **12 rutas**: cinco plantillas HTML de operadores, CSS F04 y CSS premium segregado, modelo TS con cinco styleUrls, especificación Playwright F04, auditoría premium A/B/C, este documento y nuevo workflow CI. Ni frontend genérico, ni backend, base de datos, API, contratos de autorización, roles, cookies, MFA, Documento Maestro, rama `dev`, `main`, SOLQARYN, VPS ni producción se cambian.

Base SHA fija: `31ff32d348606e77d311b6250a0e341eb052485a`. Cualquier divergencia del diff es NO-GO. **0 P0/P1** demostrados en el alcance de las pruebas es requisito de aprobación; no se presupone ausencia de riesgos no probados.

## 3. Auditorías obligatorias

**A / Funcionalidad:** mismo orden y estructura de etiquetas HTML, cinco experiencias, rutas y acciones exactas, captura 5×5 y regresiones de navegación.  

**B / Privacidad y seguridad:** comparativa de formularios, inputs, required, modales y handlers contra base; confirmación de identidad, MFA, revocación de sesiones, política con revisión y categorías agregadas del monitor. Negativas reales entre tenants y realm de plataforma en Angular/.NET/MySQL.  

**C / Arquitectura UX:** tokens institucionales, responsive de 320 a 1440 px, controles táctiles >=44 px, teclado, lector axe WCAG 2.2 AA, zoom 200 %, colores forzados y reduced-motion; datos sintéticos y hashes SHA-256 por captura.

CI requerida: TypeScript, Angular 22 Node24, Chromium, F04 25 capturas+25 pruebas premium, regresiones F02–F06, inventario 65 ID y 340 E2E Angular → .NET10 → MySQL8.4 LTS con 18 migraciones y reversiones. **No atribuir PASS antes de los logs de SHA exacto.**

## 4. Evaluación editorial pendiente de nuevas capturas

Notas finales exigidas: cinco calificaciones IA justificadas individualmente y >=9.5/10, sin promedio sustitutivo. Se revisarán los PNG originales a 320,390,768,1024,1440 con especial atención a casos protegidos, MFA, sesiones, monitor y privacidad. Las notas históricas 9.0–9.2 no representan el código nuevo.

**F04 se puede fusionar únicamente a su rama base candidata F06 después de CI verde y acta; no habilita C06 ni entrega productiva.** La CI F06 preexistente que compara un allowlist contra una base antigua requiere reconciliación separada, registrada en #179. No relajar esa barrera en este trabajo de F04.

### Reparación del presupuesto CSS

La primera CI F04 #45183537220 aprobó A/B/C pero falló al compilar Angular: el CSS combinado del componente superó el máximo de 22 KB (23.59 KB compilados). Se conserva el límite; se separa exclusivamente la nueva capa premium en `platform-operator-f04-premium.component.css` cargada por las cinco vistas de plataforma desde `platform-operator-views.component.ts`. CSS original restaurado con solo una nota de localización. Ninguna regla global o seguridad fue modificada. Nueva CI obligatoria antes de calificar.

### Corrección de regresión de token visual F02/F06

La CI #45183981603 tuvo 59/59 F04 PASS, 25 PNG/JSON verificados, F03/F02 y F05 PASS. La regresión F06 presentó **171 PASS y 3 FAIL** en la escena sintética `operator-error` a 768, 1024 y 1440 px: la tarjeta de consulta exacta F04 usaba `--cs-color-surface` blanco en lugar de `--cs-color-surface-soft`, obligatorio en F02 y en F06. Se cambia **el CSS del producto** para conservar el token institucional y el borde de jerarquía; no se modifica la prueba F06, no se oculta ningún contenido y backend/MySQL no se tocan. La CI completa debe aprobarse sobre el nuevo SHA.

## 5. Dictamen editorial y técnico final — 23-11-2026

**Identidad de código:** `595a2f9892706a24f046cd3958699d81147ee961`, rama `feature/c05-ux-f04-premium-9p5-20261123`, PR [#182](https://github.com/crecisol-platform/CRECISOL/pull/182), base `31ff32d348606e77d311b6250a0e341eb052485a`.

### 5.1 Pruebas y seguridad ejecutadas

| Puerta | Evidencia sobre producto SHA 595a2f98 | Estado |
| --- | --- | --- |
| CI push | [#45185356481](https://github.com/crecisol-platform/CRECISOL/actions/runs/45185356481), navegador + E2E real | 2/2 SUCCESS |
| CI PR | [#45185371613](https://github.com/crecisol-platform/CRECISOL/actions/runs/45185371613), navegador + E2E real | 2/2 SUCCESS |
| Auditorías A/B/C originales y premium | Cinco áreas, comparación estructural del DOM/formularios, MFA, decisiones, sesiones, políticas, allowlist de monitor y diff restringido | 6 gates PASS |
| Angular 22 y Node 24 | TypeScript, build de producción, `institutional-harness`; CSS bajo el presupuesto Angular sin elevar límites | PASS |
| Playwright F04 | **59** casos, incluidas 25 comprobaciones premium en cinco resoluciones | PASS |
| Regresiones | F03 cuenta 83, F03 navegación 18, F02 11, F05 61, F06 174 e inventario 65×5 con 30 casos | 377 PASS |
| Total navegador | **436 pruebas Playwright** | PASS |
| Integración real Angular/.NET10/MySQL8.4 | **340 comprobaciones reales**, incluidas negativas entre tenants, CSRF/cookies, roles, sesiones, doble revisor, MFA TOTP SHA1, recuperación y passkeys | PASS |
| Migraciones desechables | 18 hacia delante, 18 en reversión | PASS |
| Capturas nuevas | 5 áreas × anchos 320/390/768/1024/1440 = **25 PNG y 25 JSON**; 25/25 SHA-256 PNG contra manifiesto del SHA, **sin overflow horizontal superior al umbral de prueba** | PASS |

Artefacto [#14824610544](https://github.com/crecisol-platform/CRECISOL/actions/runs/45185371613/artifacts/14824610544), correspondiente al producto SHA indicado. Se extrajeron e inspeccionaron las capturas móviles y de escritorio, incluidos los diseños de consulta de caso, sesión, perfil, seguridad y monitor. Son datos sintéticos. El paquete RAR4 con PNG individuales, manifiesto y archivo de instrucciones fue verificado por contenido y hashes contra los PNG de la CI.

### 5.2 Evaluación editorial IA por área — criterio de compra

La rúbrica editorial considera distribución, legibilidad, respuesta móvil, jerarquía de botones, claridad de consecuencias, privacidad y consistencia institucional F01/F02/F03. Es **valoración de diseño del asistente**, no una puntuación generada por las pruebas ni aceptación humana.

| Área | Nota IA /10 | Justificación |
| --- | ---: | --- |
| **Casos** | **9.5** | Entrada de referencia primaria reconocible; información del caso y plazo separados; revisión de identidad y emisión de parte como tareas protegidas distintas, sin exposición de casos ajenos |
| **Sesiones** | **9.6** | Dispositivos e historial con lectura natural, referencia breve y revocación visiblemente destructiva; rotación individual y cierre global claramente separados |
| **Perfil** | **9.5** | Tarjeta de identidad institucional, idioma y zona horaria visibles, edición y fotografía diferenciadas, sin confundir perfil con autorización |
| **Seguridad** | **9.5** | Políticas del servidor distinguibles de las tres credenciales protegidas; vencimiento por inactividad separado, obligatorio MFA y controles de autorización preservados |
| **Monitor** | **9.6** | Señales agregadas ordenadas por gravedad y contador dominante; sin imprimir códigos internos, IP, nombres de clientes ni identificadores privados |
| **Mínimo individual** | **9.5** | **5/5 áreas cumplen el umbral editorial en el alcance revisado** |

**DICTAMEN:** F04 **APROBADA técnicamente y en evaluación editorial IA**, en código y entorno de pruebas aislado, con mínimo 9.5/10 por área y **0 P0/P1 reproducidos en los controles ejecutados**. No certifica accesibilidad humana con lector de pantalla físico, un dispositivo real, cumplimiento legal externo, aceptación del titular, `dev`, `main` ni producción.

### 5.3 Incidencia de flujo heredado, sin falsificar aprobaciones

El workflow anterior F03 [#45185371662](https://github.com/crecisol-platform/CRECISOL/actions/runs/45185371662) se ejecuta indebidamente para cualquier PR hacia la candidata F06 y falla en su **auditor estático de alcance exacto F03** al detectar los archivos deliberados de F04. No es un fallo de Chromium F03, integración ni seguridad de F04: las regresiones **83+18 de F03 sí pasaron en la CI F04**. Conservar el resultado rojo original y corregir el enrutamiento de esa puerta F03 en una microtarea de CI independiente, sin relajar su auditoría ni hacer que acepte archivos ajenos. El control F04 no queda verde por alterar tests históricos.

### 5.4 Cierre de la entrega

El PR #182 puede integrarse **solo en su rama candidata F06**, tras verificación de CI de la adenda documental. Los cambios son reversibles por Git, no crean migraciones ni alteran permisos. La rama histórica F04 PR #177 permanece abierta por trazabilidad y no debe fusionarse otra vez sobre bases anteriores. PR #172 y los bloqueos institucionales hacia `dev` continúan independientes. C06 continúa bloqueado.
