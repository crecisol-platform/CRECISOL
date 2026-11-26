# CRECISOL — Acta C06-10: pruebas ofensivas BOLA/IDOR y regresiones

**Dictamen técnico del asistente bajo delegación del propietario: 9.5/10, aceptación SÍ en el alcance delimitado de QA. Cierre institucional CONDICIONADO; NO CERTIFICADA definitivamente mientras falte comprobar la ronda POST técnica e institucional.** Fecha del dictamen: 25-11-2026 (Honduras). C06-11 bloqueada hasta ratificación comprobable. C06 global NO CERTIFICADO. Capítulos completos: 5/37.

## 1. Autoridad y alcance

Repositorio original ID 1450414592, ubicación vigente crecisol-platform/CRECISOL, rama dev; AGENTS.md, Skill canónica, MASTER_REFERENCE y registro de estado vigentes verificados. C06-09 ratificada en el registro operativo. Maestro Word/PDF SHA-256 y blob verificados por la auditoría A; 1,876 elementos originales preservados. Word SHA-256 ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c. Especificación y matriz: C06_10_ESPECIFICACION.md y C06_10_MATRIZ_REGRESION.json. Se conserva el encabezado histórico de candidata.

- Base técnica dev: 42e89226b3e53dfa8ec10da1ddf7961d4ce290f3.
- PR #216, cabeza probada: ffca485df3de5c713fbbd68598398c3194ea5420.
- Merge virtual ejecutado: 16857973bf3880748f90bf42b6c424d587bd1eed.
- Integración técnica squash exclusivamente en dev: ec0258b1d2c0738dbcb0c43aa305be18db0f9a3f.
- Árbol Git de cabeza, merge virtual e integración: 9072c2694fa6e565503ac75c562390c35af0dacc. Se verificaron los tres por API GitHub, más padre único esperado y 19 rutas exactas. Main no modificada (b794181148d2c5e36c1c88127f19a8a9082acff5). El endpoint de ramas informa protected=false; no se presupone protección.
- PRE [#45734643721](https://github.com/crecisol-platform/CRECISOL/actions/runs/45734643721) 5/5 SUCCESS; 15/15 workflows PRE SUCCESS sobre el SHA exacto. Evidencia ejecutada en runners GitHub, no en runtime local del asistente.

## 2. Cinco auditorías y resultados reales

| Auditoría | Job | Resultado y evidencia |
|---|---|---|
| A | [139624806309](https://github.com/crecisol-platform/CRECISOL/actions/runs/45734643721/job/139624806309) | SUCCESS; Word/PDF y 1,876 elementos; alcance exacto, inventario y cobertura |
| B | [139624806620](https://github.com/crecisol-platform/CRECISOL/actions/runs/45734643721/job/139624806620) | SUCCESS; 66 mutaciones estructurales, 255 contratos de privacidad, 80 de entrega, 33 de consultas |
| C | [139624806686](https://github.com/crecisol-platform/CRECISOL/actions/runs/45734643721/job/139624806686) | SUCCESS; compilación .NET 10 Release con warnings como errores, Angular 22/Node 24, validación de arnés |
| D | [139624806571](https://github.com/crecisol-platform/CRECISOL/actions/runs/45734643721/job/139624806571) | SUCCESS; MySQL 8.4 real, tres FK 1452, una unicidad 1062, cinco privilegios 1142, datos y grants preservados |
| E | [139624806532](https://github.com/crecisol-platform/CRECISOL/actions/runs/45734643721/job/139624806532) | SUCCESS; Angular/Chromium/Kestrel/MySQL reales, 228/228 celdas negativas, 921 comprobaciones integradas, 862 comprobaciones HTTP, 73 formas de éxito, 159 respuestas browser-header, 150 observadas en relay real y dos enrolamientos MFA |

Otros 14 workflows PRE con conclusión SUCCESS: 45734644050, 45734643802, 45734643771, 45734643912, 45734643828, 45734643722, 45734644009, 45734643319, 45734643348, 45734643260, 45734643426, 45734643259, 45734643273, 45734643269. No sumar comprobaciones superpuestas como casos independientes.

## 3. Seguridad, aislamiento y conservación

79 operaciones de identidad, 57 autenticadas (55 bearer y dos restauraciones browser), 22 pruebas independientes de preautenticación/proof. Para las 57: anónimo, token/cookie inventado, realm incorrecto, host incorrecto; 228 celdas HTTP 401 con recibo neutral, deltas de bitácora y controles positivos por lote. Rechazo de aprobador ajeno con factor fresco, revocación cruzada de sesiones incluso del mismo usuario, compañeros en mismo tenant, acceso a expediente sin propiedad/grant, operador plataforma, campos de autoridad manipulados. Comparación de huellas de seguridad y recursos antes/después de la matriz, preservación específica de estados/revisiones/sesiones, colisión deliberada de IDs entre empresas y denegaciones por privilegios reales. Cero accesos cruzados o escaladas exitosas **en los escenarios ejecutados**, no una garantía universal. Se excluyen de la huella cambios legítimos de cuota, auditoría y deslizamiento; algunas tablas se comprueban por cardinalidad y no por hash de cada columna. B usa sesión de fixture clonado de MFA real, no login independiente de B. No se atribuye RLS a MySQL. Las 25 acciones normativas futuras no se activan.

## 4. Corrección del fallo previo y alcance de cambios

Se conserva el fallo histórico de D [#45723923745](https://github.com/crecisol-platform/CRECISOL/actions/runs/45723923745) sobre 8205b5f174b272c1a4a87f57b6f5c9a1c0e658bd: fixture del aprobador externo colisionaba con unicidad de asignación. Se separaron controles, se exige la FK exacta del actor y aprobador, se verifica ausencia de duplicidad y se prueba por separado ERROR 1062. Corrección inicial 85910f28b1d5dea030c03066adc346fb7763adcb y ajuste de escapado Bash ffca485df3de5c713fbbd68598398c3194ea5420. La nueva D comprueba el rechazo 1452 del aprobador y las demás ocho denegaciones, sin quitar restricciones.

19 rutas exclusivamente QA, documentación y workflow; 12 QA existentes adaptados al alcance explícito del sucesor y arnés, nuevos matriz, guardas y pruebas. No cambios frontend/backend, migraciones, grants, Maestro, catálogos canónicos, producción ni SOLQARYN. Rollback: revertir el squash técnico y, si se hubiera integrado, su acta en un PR de reversión coordinado; conservar historial de pruebas. Sin migración de datos ni efecto en datos productivos.

## 5. UX y evaluación de calidad

Chromium real: acceso autorizado, sucursal incorrecta, 401 neutral, borrado de referencia privada previa, alerta accesible, modal contenido en viewport 390×844 y 1280×800 sin overflow horizontal, axe sin serious/critical en el modal y reintento autorizado correcto. No se rediseñó ninguna interfaz, no se inspeccionaron directamente capturas (el run no publicó artefactos), no se califican las 65 vistas históricas por esta prueba ni se acredita WCAG manual completo.

Valoración técnica razonada del alcance C06-10: trazabilidad/cobertura 9.5, autorización y privacidad 9.5, arquitectura/regresión 9.5, persistencia 9.5, funcionalidad/UX automatizada 9.5. Media 9.5/10. Es criterio técnico del asistente, no métrica automática de CI ni una nota editorial visual. La reserva de 0.5 refleja límites de alcance (fixtures locales, pruebas no exhaustivas, revisión visual no realizada y medición de rendimiento productivo fuera del alcance). Aceptaría la microfase de regresión en este alcance; no se certifican plataforma comercial completa ni producción. Nueva UI N/A.

## 6. Puerta estricta de cierre

Repetición POST técnica [#45737693483](https://github.com/crecisol-platform/CRECISOL/actions/runs/45737693483) sobre ec0258b1d2c0738dbcb0c43aa305be18db0f9a3f, con regresiones asociadas: pendiente al redactar; no se cuenta como aprobada. Acta institucional en rama propia con validación A/B/C, sin tocar producto. Para declarar C06-10 definitivamente CERTIFICADA se exige verificar POST técnico aplicable sin fallos, PRE documental, integrar esta acta y verificar POST documental; no basta una etiqueta de CI. Si aparece una regresión esencial, bloquear, corregir y repetir; P0/P1 abiertos impiden la certificación. Hasta entonces C06 9/11, C06-11 bloqueada. Solo después del gate C06 10/11 y C06-11 habilitada para auditoría integral, sin anticipar C07 ni certificar C06 global. La comprobación final registrará SHA y run IDs efectivos; ningún resultado pendiente se declara SUCCESS.
