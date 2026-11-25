# C06-08 — Aislamiento de consultas, listados y paginación
Estado: CANDIDATA TÉCNICA NO CERTIFICADA. Base dev ff5de0670d5a09e31b32c3b6aff7e78c5449fee8. Prerrequisito C06-07 ratificado 9.5/10: POST técnico 14/14 SUCCESS, documental 6/6 SUCCESS. Maestro Word/PDF inalterado. Únicamente CRECISOL, rama feature/c06-08-scoped-queries.

## Inventario y decisiones
| Operación existente | Ámbito obligatorio | Límite/orden |
|---|---|---|
| memberships/pending | host, tenant, actor, membresía activa, términos, grant enterprise vigente exacto; nunca rol declarado por cliente | 12+1, created_at_utc y membership_id; cursor HMAC versionado, 600s, vinculado a host, tenant, actor, membresía, sesión, grant y revisiones |
| sessions | tenant, actor, membership_id, revisiones, factor activo, ventana de inactividad | 4096 contrato existente; SQL LIMIT 4097 para detectar exceso sin truncamiento, last_seen + id |
| browser/restore | misma sesión revalidada | no recupera inventario; recibo de transporte cerrado existente |
| platform/sessions | operador autenticado exacto y revisión | 4096 contrato, LIMIT 4097, last_seen + id |
| security/events | tenant + actor propio, o sujeto propio sin actor | 50, created_at + audit_id |
| passkeys | cuenta autenticada global, no credenciales de terceros; diseño C05 cuenta compartida entre pertenencias | 8 máximo, SQL 9 para detectar exceso; opciones/conteo 8 para denegar registro adicional |
| platform/abuse-summary | exclusivamente operador de plataforma, agregado global por cuatro clases permitidas, sin tenant ni identidad individual | ventana 24h, orden por clase, 4 grupos |
| platform/cases/find | búsqueda exacta de caso por correo HMAC y realm, MFA fresco y motivo C06-05; no búsqueda libre ni listados | resultado único, sin ampliar autoridad |
| assisted reviews/shares | caso y tenant exactos, o caso de operador aislado | LIMIT 3 para preservar invariante de exactamente dos sin lectura ilimitada |

No nuevos endpoints, parámetros de búsqueda, offsets, filtros, conteos globales tenant ni grants. Los campos inesperados se rechazan en API. No se promete paginación nueva en los inventarios de sesiones: se conserva el contrato existente y su máximo; el único contrato paginado vigente (buzón) se endurece. La paginación de un inventario masivo de sesiones requeriría un cambio coordinado de contrato/UI y revisión adicional antes de certificar tal capacidad. Si una cuenta supera el máximo contractual, no se devuelve un inventario parcial; la revocación global conserva su operación independiente. No extender a expedientes, exportaciones o logs de C06-09. Sin modificaciones visuales, C05 mantiene las pantallas; regresiones Chromium obligatorias.

## Persistencia
Migración 023 exclusivamente cinco índices compuestos, sin DDL destructivo ni nuevos permisos; rollback elimina solo los índices. No se fuerza un plan en producto; EXPLAIN de laboratorio comprueba posibilidad de uso, no SLA ni escala productiva. Cursor no autoriza, no contiene PII, no es cifrado; posición ya entregada al solicitante, integridad ligada al contexto. Revalidación del grant bajo lock y del contexto en cada página. Vista viva, sin snapshot multiversión: cambios autorizados posteriores pueden retirar solicitudes; no se duplica por desplazamientos de offset.

## Gates
A: Word/PDF y catálogo inalterados, diff exacto. B: pruebas de codec real, negativos y contrato C06-07. C: .NET10, Angular22/Node24, separación y revisión de regresiones. D: MySQL8.4 real, índices, aislamiento, privilegios y rollback. E: Chromium/Angular/Kestrel/MySQL, dos tenants activos y mismo actor con sesiones independientes, páginas con fechas iguales, replay cruzado, revocación lógica de continuación mediante revisión, filtros no autorizados, todas las pruebas C05-C06-07. Auditorías anteriores conservan sus invariantes; extensión de rutas exactas solo para este sucesor. Las pruebas automatizadas aún NO EJECUTADAS en este SHA. Calificación 9.5/10 y aceptación como comprador PENDIENTES de resultados reales. No fusionar sin resultados, no adelantar C06-09. No producción, SOLQARYN ni main.
