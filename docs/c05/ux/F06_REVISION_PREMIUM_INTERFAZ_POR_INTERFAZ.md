# CRECISOL C05 UX F06 — Evaluación premium, interfaz por interfaz

**Fecha de apertura:** 20-11-2026. **Estado:** CORRECCIÓN VISUAL / PENDIENTE DE VALIDACIÓN SOBRE EL NUEVO HEAD.
**Base técnica certificada (histórica):** SHA `60601d015bf1b48c4fb308eb85a1b96a94b19cdd`; PR #179 DRAFT. La puntuación inicial de compra fue 7.9/10 sobre muestras, consola de operadores 7.1/10.

## Regla de compra y evidencia

Las 14 interfaces se prueban individualmente en cinco resoluciones (320/390/768/1024/1440 px). Cada una genera un PNG y un JSON propio, sin mosaicos: 70 capturas premium adicionales más 30 capturas de estados F06. La comparación por navegador no sustituye revisión estética personal ni lector de pantalla. **Ninguna superficie recibe 9/10 automáticamente por pasar CI**.

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
- NO_GO si alguna vista queda por debajo de 9/10 en aceptación visual comercial; la antigua certificación técnica queda limitada a su SHA histórico.

Sin producción, VPS, cobros, fusión, `dev`, `main` o escrituras a SOLQARYN. El capítulo C06 permanece bloqueado.
