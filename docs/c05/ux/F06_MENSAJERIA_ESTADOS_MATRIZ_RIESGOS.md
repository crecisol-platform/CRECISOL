# CRECISOL · C05 UX F06 — Mensajería, estados y microinteracciones

**Fecha:** 20-11-2026, Honduras. **Estado:** EN DESARROLLO / NO CERTIFICADA hasta ejecutar CI y A/B/C sobre el SHA exacto.

## Identidad y base
Proyecto exclusivamente CRECISOL, repositorio vigente `crecisol-platform/CRECISOL` ID 1450414592. Esta **Fase 06 UX pertenece a C05** y no abre el capítulo C06, que continúa bloqueado. Rama `feature/c05-ux-f06-mensajeria-estados-20261120` hija de F05 PR #178 SHA `b576d6ff64aea7d417c59aa851618dbf4dced68a`. F01–F05 y PR #172 permanecen como cadena de borradores sin fusión.

Autoridad: Documento Maestro Único, `AGENTS.md`, `.agents/skills/crecisol-project-governance/SKILL.md`, `docs/MASTER_REFERENCE.md`, `docs/ESTADO_VIGENTE_CAPITULOS.md`. Fuera de alcance: autorización backend, persistencia, MySQL/DB/migraciones, nuevos endpoints, producción, VPS, cobros, SOLQARYN, cambios en `dev`/`main`.

## Matriz funcional de estado visual

| Estado | Naturaleza | Regla |
|---|---|---|
| Pendiente | Encabezado contextual + marcador discreto; `role=status`, `aria-busy=true` | Iniciar antes de la promesa; jamás afirmar éxito |
| Completado | Mensaje concreto solo tras resultado de API | Respetar operación y ámbito originales |
| Error | `role=alert`, lenguaje público neutro | Nunca imprimir excepción, PII, secretos ni respuesta cruda |
| Campo inválido | Nota local `aria-describedby` y `aria-invalid` | Validación nativa; no conceder autorización ni enviar automáticamente |
| Cambio de vista | Revoca aviso anterior y guarda de generación | La respuesta tardía no resucita mensajes obsoletos; códigos de respaldo recién emitidos se conservan hasta guardarlos |
| Formulario sensible | Limpia contraseñas, MFA, tokens y partes tras éxito **o error** | Conserva solo correo/razón no secreta para corrección |
| Modal privilegiado | Recibos ligados a la operación real de ese modal | Sin falso éxito de asistentes, sin Escape con operación en curso |

Mensajería compartida `cs-operation-feedback`, panel institucional en estado y error. Los estilos no dependen de fuentes ni APIs de terceros. Transición `160ms` sin movimiento intrusivo; `prefers-reduced-motion: reduce` apaga todas las animaciones y `forced-colors` mantiene bordes visibles.

## Riesgos y defensas
- **P0-1** Recepción tardía al cambiar sección: secuencia monotónica `feedbackGeneration` y clave de vista/panel; invalidar al navegar antes de renderizar.
- **P0-2** Contraseña, tokens MFA o partes en DOM tras fallo de red: limpieza `clearSensitiveInputValues(form)` en `finally`, sin conservar en `localStorage` o mensajes. **Excepción UX segura:** los códigos de respaldo MFA recién emitidos deben presentarse en Resumen para que el propietario pueda guardarlos, y desaparecen con el botón «Ya guardé mis códigos», al vencer cinco minutos o al cerrarse la sesión; nunca se descartan silenciosamente entre pestañas.
- **P0-3** Confundir contexto global de operador con tenant: controladores de avisos separados por componente y no usar señales como permisos.
- **P1-1** Notificaciones duplicadas: un bloque semántico por superficie; el modal recibe solo el resultado de su propia operación.
- **P1-2** Etiquetas/español mal codificado: auditoría explícita UTF-8 en todos los archivos modificados; textos locales no devuelven mensajes de excepción.
- **P1-3** Acumulación de errores: directiva delegada por formulario restituye ARIA y elimina notas al corregir, resetear o desmontar.
- **P1-4** 320px, zoom 200%, rotación y movimiento: geometría de viewport, axe WCAG AA, contraste forzado, reduced motion y revisión de PNG individuales.
- **P1-5** Falsos éxitos en recuperación/MFA: acciones complejas solo dan instrucciones verificadas; los formularios preservan contratos .NET.

## Puertas de certificación
1. Auditoría A, B, C de F06 y F05 de regresión: PASS, diff limitado, caracteres correctos.
2. Angular 22/Node 24 compilación de producción y harness; Chromium F06 30 capturas (6 escenarios × 5 anchos) + suite funcional, F05 60, F04 34, F03 70, F02 11 y modales C05 6.
3. `qa/c05_ux_f06_feedback_evidence.py`: 30 PNG/JSON individuales, 0 hash faltante, SHA GitHub exacto. Los artefactos serán individuales y se empaquetarán en RAR auténtico para inspección humana, sin mosaicos.
4. Ejecución real Angular→API .NET 10→MySQL 8.4 desechable con seguridad multitenant, MFA, revisiones, revocaciones y 340 aserciones C05 vigentes.
5. Dictamen independiente A/B/C y acta sobre HEAD final. No certificar un cambio nuevo por la CI de F05. Si se cambia HEAD, repetir toda la ejecución.

**Rollback:** revertir la rama F06 sobre F05; no hay migraciones. **Limitaciones:** las pruebas sintéticas no prueban autorización backend, y ningún resultado autoriza producción ni fusionar la cadena de PRs automáticamente.
