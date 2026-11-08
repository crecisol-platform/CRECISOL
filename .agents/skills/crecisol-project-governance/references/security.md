# Seguridad CRECISOL — pruebas mínimas

## Tenancy
Usar identidad verificada, pertenencia vigente, TenantContext, branch, recurso, acción, propiedad y entitlements. Respuestas HTTP, caché, CDN, WebSocket, QR, documentos, reportes, notificaciones y workers deben reutilizar el mismo aislamiento.

Probar usuario A contra objeto B: cambiar path/id/header/host/slug/cookies, falsificar claims, usar recurso antes/después de suspender membresía, exportar o descargar medio B, contaminar caché y consumir evento de otro tenant. Exigir denegación y ausencia de datos. Auditorías B no visibles a A.

## Login y cuentas
No selector en login normal; resolución contextual del backend. Usuario multimiembro sin contexto inequívoco: no elegir arbitrariamente ni revelar lista. Registro de SuperAdmin solo provisioning controlado. MFA/passkeys, recuperación de un uso, revocación efectiva, cookies seguras, CSRF/XSS/IDOR/rate limits y subida de foto con inspección y autorización.

## SuperAdmin
Funcionalidad administrativa global sí; lectura privada ordinaria no. Conceder acceso excepcional temporal justificado, reautenticado, limitado, trazable y revocable; registrar actor, objetivo, tenant, motivo, hora y resultado.

## Seguimiento universal
Cada expediente cuenta con eventos de auditoría inmutables y visibilidad diferenciada cliente/empleado. Presupuesto versionado y decisiones sobre versión exacta; rechazos o autorizaciones parciales explícitas; no completar trabajo ni cerrar caso hasta cumplir criterios de entrega/resolución. Archivos con preview y acceso seguro.

## Evidencia
Tratar pruebas no ejecutadas como pendientes. Riesgos P0/P1 bloquean certificación. Medir impacto y red team proporcional antes de operación real.
