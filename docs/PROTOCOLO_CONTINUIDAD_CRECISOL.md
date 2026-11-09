# CRECISOL · Protocolo de continuidad autónoma — DISEÑO NO ACTIVADO

**Estado: DOCUMENTADO, NO DESPLEGADO, NO PROGRAMADO.** Este documento no instala ni enciende agentes o tareas. Queda subordinado al Documento Maestro Único, \`AGENTS.md\`, \`.agents/skills/crecisol-project-governance/SKILL.md\`, \`docs/MASTER_REFERENCE.md\` y las actas oficiales. Repositorio exclusivo: \`solqaryn-platform/CRECISOL\`. **SOLQARYN: escritura absolutamente prohibida.**

## 1. Decisión de seguridad y frecuencia

La propuesta del propietario solicitó tres tareas idénticas a los minutos 05, 25 y 45 para simular frecuencia de 20 minutos. **NO IMPLEMENTAR:** la herramienta de automatizaciones de ChatGPT disponible en este contexto declara **frecuencia máxima una vez por hora por tarea**; crear tres tareas desfasadas para eludirla no es una solución admitida. Además, tres procesos escritores compitiendo por \`dev\`, PR y documentación pueden producir duplicados o cierres no basados en el último SHA. **No se creó ninguna tarea.**

**Diseño candidato:** un solo agente habilitado con una tarea por hora, tras autorización del propietario para esa configuración y verificación efectiva de que puede acceder a GitHub privado, ejecutar toolchains y leer logs. Crear una tarea por sí solo no demuestra un runner de código, ni acceso persistente, ni una sesión de GitHub; no afirmar que trabaja sin comprobar los permisos y la ejecución. La única concurrencia segura es la que garantice un lock probado; una segunda ejecución solo puede observar y salir. No activar otros agentes/sistemas/servicios de pago sin autorización.

## 2. Reconstrucción desde fuentes inmutables

En cada activación:
1. Identificar el repo CRECISOL, acceso autorizado, \`dev\`, \`main\`, commits, PR abiertos, workflows, permisos y estado de bloqueos. No confiar en el recuerdo de la conversación.
2. Leer \`AGENTS.md\`, Skill propia **del GitHub vigente**, Documento Maestro Único Word y su SHA en \`docs/MASTER_REFERENCE.md\`, plan 37, \`docs/C01_PREPARACION.md\` si aplica, matriz, decisiones, acta y contrato de la microfase activa. Ante diferencias con Skill instalada, prevalece GitHub y debe registrarse.
3. Verificar qué microfase fue **cerrada con acta + SHA de merge + CI posterior**. Si hay rama/PR abierta o CI pendiente, continuar esa microfase. No inferir nueva fase por número de mensaje.
4. Revisar PR #10 \`feature/c01-identidad\` como trabajo históricamente adelantado: **NO fusionarlo ni usarlo como sustituto del orden operativo.**
5. Comprobar si existe un hallazgo P0/P1, dependencia no resuelta, activo gráfico no aprobado o aceptación de capítulo pendiente. Marcar \`BLOQUEADO\` si corresponde.

## 3. Un solo escritor y recuperación

**Estado de diseño pendiente de implementar y probar, sin lock activo hoy:**

- Fuente de ejecución: registro versionado por el orquestador con \`chapter\`, \`microphase\`, \`phase_status\`, \`owner_id\`, \`run_id\`, \`lease_epoch\`, \`lease_expires_at\`, \`heartbeat_at\`, \`branch\`, \`pr_number\`, \`expected_dev_sha\`, \`head_sha\`, \`ci_runs\`, \`audit_results\`, \`blocked_reason\`, \`next_action\`. Nunca secretos ni credenciales.
- Adquisición exclusiva con compare-and-swap real y **token de fencing** incremental; antes de CUALQUIER escritura/merge comprobar titularidad, token vigente, SHA esperado y ausencia de otro escritor. Un archivo JSON sin operación atómica no constituye un mutex.
- Heartbeat con lease y recuperación tras caída. **La expiración por sí sola no autoriza tomar control**: comprobar de forma independiente si hay job vivo y usar CAS para nuevo epoch; un antiguo agente debe quedar impedido de escribir aunque despierte más tarde.
- Ningún agente puede forzar push, resetear \`dev\`, cerrar PR ajenos o sobrescribir commits cuando cambió el SHA. Una ejecución secundaria o sin lock únicamente lee/audita y termina.
- Cuando no exista un mecanismo de lock y fencing **realmente probado**, NO lanzar dos procesos escritores, ni siquiera con tareas escalonadas. Preferir una sola ejecución programada y guardas locales de SHA; si el mismo agente se solapa, salida segura.
- Registro de cada hito: entradas verificados, commit, PR, trabajos CI y fallos; recuperación idempotente. No crear una nueva rama/PR por cada activación.

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

Estados permitidos por microfase: \`PENDIENTE\`, \`EN DESARROLLO\`, \`PROBADA\`, \`BLOQUEADA\`, \`CERRADA TÉCNICAMENTE\`. Estados de capítulo: \`PENDIENTE\`, \`EN DESARROLLO\`, \`BLOQUEADO\`, \`CERTIFICADO CON ACEPTACIÓN\`.

Reporte de cierre: capítulo y microfase, cambios efectivos, PR/commits, CI del HEAD y post-merge con enlaces, pruebas positivas/negativas, A/B/C, fallos corregidos, riesgos, rollback, progreso microfases X/Y, capítulos certificados X/37 y siguiente etapa habilitada. No afirmar seguimiento de fondo ni automatización activa sin un identificador real de tarea y evidencia de ejecución.

## 8. Estado de referencia al redactar este diseño

\`dev\`: \`33b1f74cb3d5b1fa3165226e0c0f46d2f1af5191\`, fundación SUCCESS. C01-01...C01-05 cerradas, **5/8**; C01-06 pendiente y no iniciada; C01-07/08 pendientes; **0/37** capítulos certificados. Los planes C02–C37 son PROVISIONALES, nunca habilitados. Antes de operar hay que releer \`dev\` porque este SHA envejece.
