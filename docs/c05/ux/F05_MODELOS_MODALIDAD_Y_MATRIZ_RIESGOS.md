# CRECISOL C05 UX — F05 Modalidad institucional y procesos seguros

**Alcance:** fase F05 del rediseño complementario de C05 (NO capítulo C06).  
**Estado inicial:** EN DESARROLLO / NO CERTIFICADA. **Fecha:** 19 de noviembre de 2026 (Honduras).

## Autoridad y base
- Repositorio actual `crecisol-platform/CRECISOL` ID 1450414592, transferencia histórica de `solqaryn-platform/CRECISOL`, no confundir con el repo prohibido SOLQARYN.
- Autoridad de `AGENTS.md`, Skill `.agents/skills/crecisol-project-governance/SKILL.md`, `docs/MASTER_REFERENCE.md` y `docs/ESTADO_VIGENTE_CAPITULOS.md` de `dev`.
- F05 parte de F04, PR #177, SHA **7d66d011abb5183e5ebeb625b85be176e079638f**. CI #44680701140 (2/2 SUCCESS), 117 pruebas Chromium y 340 E2E reales; F01..04 tienen cierre técnico limitado, pero dependen de PRs sin fusionar.
- Trabajar exclusivamente en `feature/c05-ux-f05-modales-procesos-seguros-20261119` y PR DRAFT con base F04. Sin despliegues, `dev`, `main`, Word/PDF Maestro, VPS, DNS, pagos o SOLQARYN.

## Contrato visual, navegación y seguridad
`cs-institutional-modal` usa `<dialog>.showModal()`, top-layer nativo, contenido de fondo inerte, retorno del foco al lanzador, bloqueo de Escape/cierre durante mutaciones, reset de formularios al finalizar y sin doble superposición. El frontend no concede permisos.

| Patrón F05 | Uso | Regla |
| --- | --- | --- |
| **Simple** | consulta informativa o renovación controlada | lectura clara, cancelación sin acciones automáticas y mensajes de alcance |
| **Formulario** | credenciales, perfil, invitaciones, registro de passkey | datos mínimos, validación nativa, mensajes de respuesta de API, no acciones al abrir |
| **Destructivo / sensible** | revocar llaves/sesiones, aprobación/rechazo, emisión de partes | advertencia contextual, elemento seleccionado, decisión explícita y MFA/confirmación según backend |
| **Asistente de varios pasos** | recuperación global de operador con doble control y MFA, reemplazo de MFA de operador | contador `aria-current=step` progresivo; avance por estado recibido de API o intención explícita, sin saltarse servidores |

**Fronteras:** los flujos de negocio permanecen vinculados al tenant autenticado; los operadores utilizan `/api/v1/platform-identity/` y credenciales/CSRF de realm distintos. MFA SHA1 y recuperación asistida conservan los mismos contratos criptográficos. Los diálogos no validan un permiso de servidor ni reemplazan auditoría, controles de cuotas, expiración o protección BOLA.

## Riesgos y mitigaciones
1. **Riesgo P0: confundir éxito global con el modal actual.** Solo `submit` del formulario o botón de comando con `data-modal-action` vincula la respuesta a ese diálogo. Los botones de paso o para mostrar QR no lo hacen.
2. **Riesgo P0: sobreposición o fuga de secretos.** Solo una top-layer; los campos se restablecen al cerrar, y los secretos temporales siguen limpiándose en los listeners `dismissed` de los flujos existentes.
3. **Riesgo P1: recorte de formulario en zoom/teclado móvil.** Contenedor con límite `dvh`, cabecera/pie independientes y cuerpo desplazable, sin scroll horizontal. Probar 320/390/768/1024/1440 y 200 %.
4. **Riesgo P1: falso flujo de pasos.** Etapas de recuperación solo muestran formularios pertinentes; la fase 3 aparece únicamente cuando el servidor emitió el secreto de enrolamiento. No presentar un paso "aprobado" por clic local.
5. **Riesgo P1: permisos mezclados.** Mantener API, tenant, MFA y contextos intactos; probar negativas reales en .NET10/MySQL8.4.

## Puertas de auditoría y entregable
- **A**: componentes, contratos, tipos, compilación Angular 22 y regresiones F04/F03/F02.
- **B**: denegación por defecto, diálogos únicos, focus/cancel, campos limpiados, MFA, revocaciones, dos revisores, BOLA, CSRF y sesiones en E2E real.
- **C**: WCAG axe, accesibilidad teclado, 200 % texto, tamaño mínimo, landscape/forced-colors, 55 PNG/JSON individuales, integridad SHA-256 exacta.
- Conservar todos los fallos históricos como evidencia; no marcar como PASS una prueba anterior que falló. Cada nuevo HEAD requiere CI propia 2/2 SUCCESS y 340 pruebas E2E reales antes de certificarse.
- Paquetes de imágenes para revisión del propietario: **PNG individuales a tamaño original dentro de RAR genuino**, nunca mosaicos.
- Dictamen final requerido: evaluación editorial individual de escenarios y acta de SHAs, runs, riesgos y restricciones. Sin firma humana afirmada por el asistente.

**Rollback:** revertir F05 sobre su base F04; sin migraciones nuevas ni cambios de backend.
