# CRECISOL · C05 UX F04 — Acta de revisión y certificación técnica delimitada

**Fecha del dictamen:** 19 de noviembre de 2026 (Honduras) / 20 de noviembre UTC.  
**Ámbito:** Fase 04 del rediseño C05, **consola de operadores globales**. No es una certificación del capítulo C06 ni de producción.

## 1. Autoridad, repositorio y alcance

Repositorio vigente **crecisol-platform/CRECISOL**, ID inmutable `1450414592`; validado mediante `AGENTS.md`, Skill `.agents/skills/crecisol-project-governance/SKILL.md`, `docs/MASTER_REFERENCE.md`, estado `dev` y plan de 37 capítulos. La ubicación histórica `solqaryn-platform/CRECISOL` está superada por la transferencia registrada; **SOLQARYN no se escribió ni se utilizó como componente técnico**.

PR **#177 DRAFT**, rama `feature/c05-ux-f04-consola-operadores-20261119`, base `feature/c05-ux-f03-arquitectura-navegacion-20261119` (PR #176, HEAD fuente F03 `b503c97a0cae82b3c4c60853e084286c79073943`). Cadena previa #172 (MFA SHA1/30 min), #174 (F01), #175 (F02) y #176 (F03) permanece sin fusionar. **Sin push a dev/main ni despliegues.**

## 2. Cinco experiencias administrativas y límites del backend

| Vista | Funciones y controles verificados | Frontera |
| --- | --- | --- |
| Casos | Consulta por referencia, estado, prioridad, selección explícita aprobar/rechazar, motivo, evidencia SHA-256, confirmación y MFA; entrega de partes en diálogo privado | No existe API de bandeja global autorizada: no se inventa búsqueda universal de cuentas o empresas |
| Sesiones | Familia de navegador y fechas legibles, referencia recortada, filtros por actividad/caducidad, revocación individual y confirmaciones; cierre global con MFA | Identidad, cookie, token y CSRF de plataforma independientes de negocio |
| Perfil | Nombre/idioma/zona/foto, estados de información consultada, edición precargada, botón con nombre accesible estable | El perfil no modifica roles ni incorpora datos de un tenant |
| Seguridad | Contraseña, correo verificado, reemplazo TOTP SHA1, política de inactividad 30 minutos administrable con MFA y revisión | Permisos de administración de política solo los determina el backend; una denegación elimina la política visual anterior |
| Monitor | Contadores globales agregados de 24 horas, filtros de atención alta/media/informativa y categorías desconocidas agrupadas | No expone IP, correos, clientes, UUID, huellas de dispositivos ni códigos internos de backend |

Las etiquetas, filtros y alertas son **presentación**, nunca autorización. Los casos de identidad requieren soporte auditado, ámbito explícito, dos operadores distintos para recuperación asistida y período de seguridad exigido por servidor.

## 3. Evidencia ejecutada sobre SHA de código anterior a esta formalización

**SHA ejecutable auditado:** `b880aabd964d355b72fee3851b61377ffeb8fed8`.  
[GitHub Actions #44678652323](https://github.com/crecisol-platform/CRECISOL/actions/runs/44678652323): **completed/success; 2/2 jobs SUCCESS**.

| Puerta | Resultado |
| --- | --- |
| Auditoría A — fuente, rutas, contratos y compilación | **PASS**, TypeScript y Angular 22 producción, F02 harness, .NET 10 API/provisionador/SMTP de prueba sin errores |
| Auditoría B — seguridad, aislamiento y privacidad | **PASS** en el alcance ejecutado, incluidos 340/340 E2E reales en Angular→.NET10→MySQL8.4, MFA SHA1, doble operador, permiso/tenant A vs B, CSRF/cookies, sesiones/rotación, auditoría y logs sin secretos |
| Auditoría C — UI, calidad y accesibilidad | **PASS** de auditor estático, axe WCAG 2.2 AA, 200 % texto/teclado/colores forzados, sin desbordamiento horizontal, 5 tamaños y regresión de diálogos |
| Chromium F04 | **33/33 PASS**; cinco vistas con datos ficticios, negativas de realm, estados, siete nombres accesibles estables y confirmaciones |
| Chromium F03 / F02 / C05 | **18 + 52 + 11 + 2 = 83/83 PASS** |
| Total Chromium | **116/116 PASS** |
| Migraciones de esquema en MySQL 8.4 desechable | **18 hacia delante y 18 reversas PASS**, sin recursos productivos |
| Capturas F04 | **25 PNG + 25 JSON**, 5 vistas × 320/390/768/1024/1440 px, manifest vinculados a SHA `b880aab`, 25/25 SHA-256 verificados de manera independiente |
| API/DB | **340/340 aserciones PASS**; no presume acceso ni cumplimiento productivo |

Antecedentes preservados: las ejecuciones fallidas anteriores de auditoría, tipado y localizadores de F04 **no** se reclasifican como éxito. En particular #44677235251 se interrumpió en el nombre accesible de Perfil tras ~290 comprobaciones; #44678652323 demuestra el arreglo con cobertura nueva y las 340 comprobaciones completas.

**Esta acta incluye dos mejoras de cierre posteriores a `b880aab`:** restitución del nombre accesible real del encabezado de Perfil y anulación segura del valor/permiso visual anterior al recibir denegación de la política de inactividad. **No se atribuyen los resultados de `b880aab` a esos cambios**; el HEAD que contiene esta acta debe ejecutar sus propios gates A/B/C, Chromium y E2E antes del dictamen definitivo.

## 4. Dictamen editorial individual sobre capturas

Rúbrica con puntuación máxima por columna: composición 2,0; jerarquía 1,5; tareas y prevención de errores 2,0; estados/redacción/privacidad 1,5; responsive/teclado/axe 2,0; consistencia institucional C01/C02–F02 1,0. Se evalúa **cada vista**, no la media.

| Vista | Comp. | Jerar. | Tareas | Estados | Responsiva/AA | Consistencia | Evaluación /10 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Casos | 1,8 | 1,4 | 1,8 | 1,4 | 1,8 | 0,9 | **9,1** |
| Sesiones | 1,9 | 1,4 | 1,8 | 1,4 | 1,8 | 0,9 | **9,2** |
| Perfil | 1,9 | 1,4 | 1,8 | 1,4 | 1,7 | 0,9 | **9,1** |
| Seguridad | 1,8 | 1,4 | 1,8 | 1,3 | 1,8 | 0,9 | **9,0** |
| Monitor | 1,9 | 1,4 | 1,8 | 1,4 | 1,8 | 0,9 | **9,2** |

**Observaciones:** Casos presenta una tarea delicada y larga en móvil, pero agrupa claramente consulta, revisión y entrega. Sesiones separa consulta y revocación sin datos cruzados. Perfil preserva jerarquía y acciones de presentación. Seguridad concentra operaciones extensas solo bajo diálogos. Monitor evita exponer códigos y ordena contadores agregados. En cinco tamaños no hubo desbordamiento del documento. **5/5 valoraciones editoriales del asistente ≥9,0/10** para los estados representados en capturas; esta nota **subjetiva** no equivale a calificación humana del propietario/Codex ni a pruebas manuales de lector de pantalla.

## 5. Eficacia, límites y reversibilidad

**Dictamen propuesto: APROBACIÓN TÉCNICA F04 delimitada a código/QA local aislada.** Su eficacia sobre el **HEAD documental posterior a este acta** queda condicionada a **CI propia 2/2 SUCCESS en SHA exacto**, pruebas negativas y ausencia de P0/P1. Registrar la ejecución y resolución final en comentario trazable del PR #177 sin alterar el resultado histórico #44678652323.

Esta certificación no implica certificación general adicional de C05, inicio de C06, autorización de negocio a operador, lista global de expedientes, pruebas productivas, aprobación humana del propietario/Codex, cumplimiento COM-T08/09/10, servicios externos, VPS, pagos, cambios a `dev/main` o escrituras a SOLQARYN. La inspección axe automatizada no reemplaza una evaluación humana de tecnologías de asistencia.

**Rollback:** revertir F04 sobre su base F03, sin migraciones nuevas ni cambios a credenciales productivas; el endpoint y la autorización del backend permanecen sin modificación.
