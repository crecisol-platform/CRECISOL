# CRECISOL C05 UX F06 — Evaluación premium, interfaz por interfaz

**Fecha de apertura:** 20-11-2026. **Actualización de criterio:** 21-11-2026. **Estado:** DEUDA PREMIUM EN RECUPERACIÓN, SIN APROBACIÓN 9.5/10.
**Base técnica certificada (histórica):** SHA `60601d015bf1b48c4fb308eb85a1b96a94b19cdd`; PR #179 DRAFT. La puntuación inicial de compra fue 7.9/10 sobre muestras, consola de operadores 7.1/10.

## Regla de compra y evidencia

Las 14 interfaces se prueban individualmente en cinco resoluciones (320/390/768/1024/1440 px). Cada una genera un PNG y un JSON propio, sin mosaicos: 70 capturas premium adicionales más 30 capturas de estados F06. La comparación por navegador no sustituye revisión estética personal ni lector de pantalla. **Ninguna superficie recibe 9.5/10 automáticamente por pasar CI**.

| Interfaz | Defecto identificado | Refinamiento F06 | Revisión visual |
| --- | --- | --- | --- |
| Iniciar sesión | Hero extenso, enlace localhost visible, opción llave secundaria poco clara | Hero compacto, correo/contraseña prioritarios, alternativa passkey contextual | Pendiente |
| Solicitar cuenta | Exceso de recorrido móvil previo al formulario | Hero compacto de cuenta anónima | Pendiente |
| Recuperación | Información previa ocupa primera vista | Hero compacto y navegación secundaria accesible | Pendiente |
| Resumen autenticado | Jerarquía y estados | Preservar contrato F03 y avisos de F06 | Pendiente |
| Perfil | Contexto y avisos | Mensajes por operación, limpieza de secretos | Pendiente |
| Llaves | Acciones protegidas | Mantener modales seguros F05 | Pendiente |
| Seguridad | Advertencias repetidas y prioridad de acciones | Error con orientación concreta, modales F05 intactos | Pendiente |
| Historial | Lectura y filtrado | Regresión visual y WCAG propia | Pendiente |
| Aprobaciones | Confirmación y densidad | Regresión visual y aislamiento de tenant | Pendiente |
| Casos de plataforma | Densidad, repetición de avisos, estado vacío anidado | Consulta primero; privacidad ampliable; estado vacío simplificado | Pendiente |
| Sesiones de plataforma | Visibilidad móvil de navegación | Indicador explícito de deslizamiento y privacidad consultable | Pendiente |
| Perfil de plataforma | Densidad en móvil | Encabezado más discreto y navegación visible | Pendiente |
| Monitor de plataforma | Densidad y prioridades | Jerarquía compacta y navegación visible | Pendiente |
| Seguridad de plataforma | Exceso de avisos | Permisos consultables, error orientado a siguiente paso | Pendiente |

### Puertas
- A: 14 superficies trazables en Playwright, cambios restringidos a frontend y QA; caracteres españoles correctos.
- B: multitenancy, doble control, MFA/passkeys, permisos y limpieza de secretos sin cambios funcionales; 340 E2E obligatorias.
- C: 70 capturas premium independientes y 30 de estados F06; ancho sin desbordamiento; contraste, 200 % zoom, movimiento reducido, axe AA, teclado y regresiones F02–F05.
- Entrega: RAR real con capturas individuales y nuevo SHA-256 verificable; informe de nota por interfaz tras ver cada imagen.
- NO_GO si alguna vista queda por debajo de 9.5/10 en aceptación visual comercial; la antigua certificación técnica queda limitada a su SHA histórico.

Sin producción, VPS, cobros, fusión, `dev`, `main` o escrituras a SOLQARYN. El capítulo C06 permanece bloqueado.


## Nuevo criterio obligatorio: compra real, mínimo 9.5/10 por interfaz (21-11-2026)

El propietario elevó el umbral de **9.0 a 9.5/10 para CADA interfaz**. No se compensa una pantalla débil con otra excelente ni se redondea 9.49 a 9.5. Una puntuación es una valoración editorial justificada después de inspeccionar PNG originales de cinco resoluciones, estados vacíos/cargados/errores, navegación real, contraste, teclado, foco, jerarquía visual, textos, privacidad, rendimiento y modales protegidos. 10/10 solo con evidencia convincente; no por alcanzar una cuota.

La última evaluación editorial sobre `ced06a9ccd756203fe1c676189612c77c43fa0bc` (14/14 entre 9.0 y 9.3) es **histórica, válida para el criterio antiguo y no suficiente para el nuevo**. Queda 0/14 aprobadas formalmente bajo la nueva puerta de 9.5; esto no revoca las 306 pruebas Chromium ni las 340 aserciones integradas anteriores. Cualquier cambio de código requiere volver a probar su SHA exacto.

| Superficie | Nota anterior /10 | Para 9.5 falta demostrar |
| --- | ---: | --- |
| Ingreso | 9.3 | Flujo inicial compacto, alternativa passkey y mensajes de error claros en móvil |
| Solicitar cuenta | 9.2 | Solicitud encontrable sin recorrido extenso, validaciones y acciones claras |
| Recuperación | 9.0 | Camino principal prioritario, alternativas seguras y lectura breve |
| Resumen de cuenta | 9.0 | Sesión y dispositivos escaneables sin duplicación de tarjetas |
| Perfil empresarial | 9.1 | Datos legibles, estados vacíos y edición sin ruido |
| Llaves de acceso | 9.0 | Registro/revocación protegidos, elección consciente y contexto útil |
| Seguridad de cuenta | 9.2 | Jerarquía de cambios sensibles y MFA con confirmaciones comprensibles |
| Historial | 9.1 | Línea de tiempo, filtros y estados localizados sin pérdida de información |
| Aprobaciones | 9.0 | Cola prioritaria, decisiones sin preselección y feedback preciso |
| Casos de plataforma | 9.0 | Consulta por referencia protagonista; no se permite enumeración de tenants |
| Sesiones de operadores | 9.1 | Navegación móvil, dispositivos y revocación legibles |
| Perfil de operadores | 9.0 | Datos/acciones agrupados y tipografía móvil legible |
| Seguridad de operadores | 9.0 | MFA/contraseña/política de sesión con prioridades claras |
| Monitor de operadores | 9.2 | Señales agregadas escaneables y ningún dato privado revelado |

### Reparaciones iniciadas en código
- Consola: compactar encabezado institucional sin sacrificar H1, jerarquía del módulo ni privacidad visible; eliminar cejas duplicadas en encabezados internos.
- Elevar tamaño mínimo de texto contextual y pistas de navegación a 14px con fuente raíz normal; conservar reflujo y zoom al 200%.
- Hacer distinguibles y accesibles las cinco áreas por pestañas, con objetivos táctiles de al menos 44px y recorrido de teclado.
- Reducir movimiento innecesario de botones frecuentes; respetar preferencias de movimiento reducido.
- Mantener todas las autorizaciones, contratos, modales MFA, recuperación y separación empresa/plataforma.

### Deudas que NO se declaran resueltas
1. Inspección visual independiente y nota comercial 9.5 individual en el NUEVO SHA, incluyendo 70 PNG originales (14×5), 30 estados y capturas de modales.
2. Pruebas con lectores de pantalla físicos, hardware móvil y clientes reales (fuera de las pruebas automatizadas).
3. Warnings CommonJS `qrcode`/`dijkstrajs`, deprecaciones de Node/GitHub Actions y aumento estimado de ~2.39 KB en transferencia inicial frente al baseline previo: requieren diagnóstico/corrección propia, sin silenciarlos.
4. Cadena PR #172, #174–#179 en DRAFT, sin integración final en `dev`. No fusionar sin autorización y validación del alcance.

**Regla de estado:** hasta 14 dictámenes visuales >=9.5/10 y A/B/C + Angular + Chromium + integración .NET/MySQL del SHA final, este trabajo sigue **NO-GO de aceptación premium**. F07 no se inicia. C06 permanece bloqueado y producción está fuera de alcance.

**Referencias originales consultadas (modo lectura, sin instalar/ejecutar scripts):**
- EXT-03 Impeccable, `pbakaus/impeccable@2149fcce39a90bb409df5f16515f316a76dc6199`, `.agents/skills/impeccable/SKILL.md`, `reference/craft-floor.md`, `reference/distill.md`, `reference/adapt.md`, `reference/critique.md`; jerarquía, reducción de ruido, responsive y criterios de compra.
- EXT-04 Motion, `emilkowalski/skills@d23d7f88a2e21c9e4b1418c7abe420f5c1052ba7`, `skills/review-animations/SKILL.md`; movimientos discretos, evitación de transformaciones gratuitas.
- EXT-10 Angular Developer, `angular/angular@6808966993bf2b2d455d9b6245e960a59a01b9a9`, `skills/dev-skills/angular-developer/SKILL.md`, `references/component-styling.md` y `references/e2e-testing.md`; estilos por componente, pruebas sin reemplazar el framework.
- EXT-06 Humanizer, `blader/humanizer@9862685f575c65a8247f90369951df1b3416e3d6`, `SKILL.md`; textos claros sin alterar mensajes de seguridad.

No se incorporó código, dependencias, herramientas ni scripts de estas referencias. La única Skill operativa local sigue siendo `crecisol-project-governance`.
