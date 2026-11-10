# CRECISOL · Protocolo de continuidad autónoma — ejecutor único con guardas SHA

**Estado histórico al redactar este protocolo: se había registrado UNA tarea horaria ChatGPT/GitHub; capacidades y desarrollo solo se acreditan por ejecución real.** Este documento no instala ni enciende agentes o tareas. Queda subordinado al Documento Maestro Único, \`AGENTS.md\`, \`.agents/skills/crecisol-project-governance/SKILL.md\`, \`docs/MASTER_REFERENCE.md\` y las actas oficiales. Repositorio exclusivo: \`solqaryn-platform/CRECISOL\`. **SOLQARYN: escritura absolutamente prohibida.**

**ESTADO GLOBAL VIGENTE VERIFICADO EN GITHUB:** C01/C02 certificados (2/37), C03-01..05 CERTIFICADAS (5/10), C03-06 NO INICIADA, C03 NO CERTIFICADO y C04 BLOQUEADO. [Constancia C03-05](c03/C03_05_CERTIFICACION_POSTMERGE.md), SHA 12fb19b0c35038c297939e17b27770c4fd63f77e. **Este archivo no acredita la vigencia ni ejecución de ninguna automatización programada**; las secciones históricas de calendario y locks requieren verificación independiente.

## 1. Decisión de seguridad y frecuencia

La propuesta del propietario solicitó tres tareas idénticas a los minutos 05, 25 y 45 para simular frecuencia de 20 minutos. **NO IMPLEMENTAR:** la herramienta de automatizaciones de ChatGPT disponible en este contexto declara **frecuencia máxima una vez por hora por tarea**; crear tres tareas desfasadas para eludirla no es una solución admitida. Además, tres procesos escritores compitiendo por \`dev\`, PR y documentación pueden producir duplicados o cierres no basados en el último SHA. **Posteriormente se creó UNA tarea recurrente, sin autorizar varios escritores.**

**Diseño candidato:** un solo agente habilitado con una tarea por hora, tras autorización del propietario para esa configuración y verificación efectiva de que puede acceder a GitHub privado, ejecutar toolchains y leer logs. Crear una tarea por sí solo no demuestra un runner de código, ni acceso persistente, ni una sesión de GitHub; no afirmar que trabaja sin comprobar los permisos y la ejecución. La única concurrencia segura es la que garantice un lock probado; una segunda ejecución solo puede observar y salir. No activar otros agentes/sistemas/servicios de pago sin autorización.

## 2. Reconstrucción desde fuentes inmutables

En cada activación:
1. Identificar el repo CRECISOL, acceso autorizado, \`dev\`, \`main\`, commits, PR abiertos, workflows, permisos y estado de bloqueos. No confiar en el recuerdo de la conversación.
2. Leer \`AGENTS.md\`, Skill propia **del GitHub vigente**, Documento Maestro Único Word y su SHA en \`docs/MASTER_REFERENCE.md\`, plan 37, \`docs/C01_PREPARACION.md\` si aplica, matriz, decisiones, acta y contrato de la microfase activa. Ante diferencias con Skill instalada, prevalece GitHub y debe registrarse.
3. Verificar qué microfase fue **cerrada con acta + SHA de merge + CI posterior**. Si hay rama/PR abierta o CI pendiente, continuar esa microfase. No inferir nueva fase por número de mensaje.
4. Revisar PR #10 \`feature/c01-identidad\` como trabajo históricamente adelantado: **NO fusionarlo ni usarlo como sustituto del orden operativo.**
5. Comprobar si existe un hallazgo P0/P1, dependencia no resuelta, activo gráfico no aprobado o aceptación de capítulo pendiente. Marcar \`BLOQUEADO\` si corresponde.

## 3. Ejecutor único: corrección del bloqueo y modo seguro

**Incidente verificado:** dos ejecuciones del 8 de noviembre de 2026 pudieron leer GitHub, pero no escribieron por exigirse un lock distribuido con fencing aún no implementado. La exigencia se mantiene para **múltiples agentes escritores**; para **UNA sola tarea programada**, se permite un modo de escritura controlada con verificaciones GitHub de SHA y sin otras ejecuciones activas comprobadas. No es un mutex global ni garantiza ausencia absoluta de otros escritores.

### Modo 1: exactamente UNA tarea ChatGPT/GitHub (autorizado)

1. Antes de escribir: verificar solo repo `solqaryn-platform/CRECISOL`, `dev`, rama feature, PR existentes, CI y actividad reciente. No ejecutar una segunda tarea escritora; si la misma tarea se ejecuta manualmente mientras otra instancia trabaja, abstenerse de escribir. Ante evidencia de solapamiento o imposibilidad material de excluirlo, modo lectura, informar.
2. Cambios de archivos: obtener `fetch_file.sha` actual y usar `update_file` con ese SHA en la rama feature. Para cambios múltiples, crear commit cuyo padre sea HEAD observado y publicar vía `update_ref(expected_sha=HEAD, force=false)`. Si hay conflicto o cambio de HEAD, no forzar ni reintentar ciegamente: reevaluar los cambios.
3. Antes del PR/merge: revisar diff, secretos, alcance, auditorías A/B/C y CI del último SHA; releer `dev` y `PR.head_sha`; verificar que la base no cambió desde la revisión. Merge exclusivamente a dev usando `expected_head_sha`, sin force. Si hay cambio concurrente de SHA, suspender ese merge y revisar otra vez. Verificar CI posterior antes de declarar cierre.
4. El control SHA es **optimista y limitado**: GitHub no transacciona el lock y el commit sobre dos ramas simultáneamente; no afirmar fencing que no existe. Detectar escritores humanos o de Actions y detener operaciones potencialmente conflictivas. No tocar `main`, PR #10 ni SOLQARYN.
5. Conservar estado en PR/actas/commits: capítulo, microfase, SHA, rama, CI, auditorías, errores, rollback y próxima acción. Retomar exactamente donde haya evidencia.
6. **No auto-desactivar por un bloqueo funcional, pendiente de aprobación o activo gráfico faltante.** Informar una vez el bloqueo concreto, mantener la tarea programada y revalidar en una ejecución posterior, sin informes idénticos repetitivos. Solo pausar por orden del propietario o incidente de seguridad grave confirmado.
7. En C01-06 se permiten preparaciones verificables como especificación, extracción de candidato del Word o pruebas negativas; no incluir un logo no aprobado ni declarar C01-06 cerrada. Si se requiere aprobación humana del archivo oficial, detener solo esa entrega y explicarlo.

### Modo 2: dos o más escritores (NO HABILITADO)

Antes de activar más de un agente escritor se requiere **mecanismo real de exclusión mutua compartida**, adquisición compare-and-swap, propietario/run_id, epoch monotónico, lease, heartbeat y fencing que impida escrituras de agentes obsoletos. Expirar un lease no prueba que el escritor antiguo murió. El plan de lock distribuido permanece **diseñado, no implementado ni probado**. En presencia de una segunda tarea, prohibir su escritura hasta certificar este mecanismo.

## 4. Ejecución completa de UNA microfase

| Paso | Trabajo de ingeniería | Evidencia para pasar |
| --- | --- | --- |
| 01 | Reconstruir repo, dev, maestro, actas y estado de PR | SHA base y microfase verdadera |
| 02 | Determinar dependencia anterior y verificar gates | Acta y CI post-merge o BLOQUEADO |
| 03 | Delimitar requisitos y criterio de aceptación | Matriz trazable, riesgos, impacto |
| 04 | Establecer planes de pruebas positivos/negativos | Casos y herramientas disponibles |
| 05 | Retomar o abrir rama feature de alcance mínimo | Base dev verificada, sin duplicados |
| 06 | Programar solo la microfase actual | Diff segregado frontend/backend/database |
| 07 | Compilar y ejecutar tests reales | Logs, timestamps y SHA coincidentes |
| 08 | Auditoría A de trazabilidad/función | Evidencias de requisitos en tests |
| 09 | Auditoría B de seguridad/tenancy | BOLA, IDOR, auth, secretos, caché, medios según alcance |
| 10 | Auditoría C de arquitectura/performance/UX | Benchmarks, accesibilidad, compatibilidad y revisión |
| 11 | Corregir fallos y reejecutar lo afectado | Fallos históricos conservados, nueva CI |
| 12 | Revisar PR, permisos, secretos y diff | 0 P0/P1, sin cambios fuera de alcance |
| 13 | Validar CI **del último HEAD** | Todos los jobs requeridos SUCCESS |
| 14 | Fusionar a dev bajo delegación vigente | Merge con SHA esperado, sin force |
| 15 | Verificar integración y CI posterior | SHA dev y CI post-merge SUCCESS |
| 16 | Actualizar contrato, acta, plan y cierre | PR documental revisado e integrado |
| 17 | Reportar y habilitar siguiente microfase | Estado en GitHub, riesgos y enlaces |

Cada microfase tiene **tres auditorías diferenciadas** A/B/C, además de pruebas positivas y negativas. El último paso **no** certifica el capítulo completo. Un error no se soluciona silenciando tests, eliminando cobertura, ignorando advertencias críticas o rebajando políticas. La ausencia de runner requerido equivale a **NO EJECUTADO**, no PASS.

## 5. Reglas de certificación de capítulo

Solo capítulo N activo. C01–C37 secuenciales, sin desarrollar código de N+1 antes del acta y aceptación de N. Al cerrar la última microfase de N:
- Verificar todo el capítulo contra **el maestro**, no solo los PR.
- Ejecutar build, pruebas de regresión e integración REAL, seguridad multitenant donde aplique y auditorías globales A/B/C.
- Bloquear si queda P0/P1 o una prueba indispensable no ejecutada. Un mock no equivale a prueba de producción.
- Preparar acta integral, riesgos, evidencia, SHA y aceptación **expresa** del propietario antes de certificar N y habilitar N+1. La delegación actual de microfases C01 **no** equivale a delegación indefinida para certificar capítulos.
- Si se requiere un proveedor, VPS, DNS, pago o integración externa expresamente prohibida hoy, detener certificación dependiente sin activar el proveedor.

## 6. Límites absolutos

Solo código, documentación, pruebas locales/CI y PR en CRECISOL. Sin despliegues, DNS, Hostinger, Cloudflare/R2 reales, PSP, cobros, compras, credenciales productivas, cambios de protección de ramas ni escrituras a SOLQARYN. \`main\` es rama de versión certificada; \`dev\` integración; trabajar sobre \`feature/cNN-xx-*\`. No crear otras Skills locales ni ejecutar referencias externas opcionales por defecto.

Backend aplica pertenencia, scope, rol, acción, sucursal y propiedad. Shared DB por tenant; dedicated DB con router protegido de servidor. El navegador nunca concede acceso privado. SuperAdmin excepcional necesita MFA, motivo, duración, revocación y auditoría. Objetivo: **cero P0/P1 pendientes para cerrar**, nunca promesa absoluta de «margen de error nulo».

## 7. Estados y reporte al propietario

Estados permitidos por microfase (incluye CERTIFICADA solo después de acta y CI verificadas): \`PENDIENTE\`, \`EN DESARROLLO\`, \`PROBADA\`, \`BLOQUEADA\`, \`CERRADA TÉCNICAMENTE\`. Estados de capítulo: \`PENDIENTE\`, \`EN DESARROLLO\`, \`BLOQUEADO\`, \`CERTIFICADO CON ACEPTACIÓN\`.

Reporte de cierre: capítulo y microfase, cambios efectivos, PR/commits, CI del HEAD y post-merge con enlaces, pruebas positivas/negativas, A/B/C, fallos corregidos, riesgos, rollback, progreso microfases X/Y, capítulos certificados X/37 y siguiente etapa habilitada. Distinguir tarea configurada, ejecución realizada y código realmente probado. No afirmar ejecución perfecta ni auto-desactivar la tarea por faltas de un activo; un bloqueo se informa sin inventar PASS.

## 8. Instantánea HISTÓRICA al redactar este diseño (no vigente)

\`dev\`: \`33b1f74cb3d5b1fa3165226e0c0f46d2f1af5191\`, fundación SUCCESS. C01-01...C01-05 cerradas, **5/8**; C01-06 pendiente y no iniciada; C01-07/08 pendientes; **0/37** capítulos certificados. Los planes C02–C37 son PROVISIONALES, nunca habilitados. Antes de operar hay que releer \`dev\` porque este SHA envejece.

## 9. Registro de reparación de auto-bloqueo

Las dos ejecuciones reales pudieron leer GitHub y localizar C01-06, pero se auto-bloquearon exclusivamente ante un requisito de mutex global aún no implementado. La edición habilita **solo modalidad de tarea única con SHA optimista**; NO certifica herramienta de compilación, integridad binaria del logotipo ni ausencia absoluta de carreras. El horario y la activación se confirman exclusivamente en la plataforma de automatizaciones, no por este texto.
