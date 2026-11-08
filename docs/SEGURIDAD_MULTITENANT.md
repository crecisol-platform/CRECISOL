# Seguridad transversal — contrato de diseño (sin certificación)

1. Shared DB: tenant_id obligatorio en entidades privadas, FK/UNIQUE compuestas, queries tenant-aware, scope de sucursal/recurso, principio fail-closed; MySQL no presume RLS nativa.
2. Dedicated DB: elección exclusiva por el router del backend a partir del catálogo confiable; sin controlar DSN con cookies o URL; mismas reglas de permisos y tests.
3. Aislamiento total de usuarios, datos, bitácoras, medios, historial, jobs, websockets, búsquedas, reportes, exportaciones, copias y restauraciones por tenant.
4. SuperAdmin puede administrar todas las funciones, pero las vistas privadas de consumidores requieren acceso excepcional con MFA, causa, duración y auditoría.
5. Login automático por pertenencia/host verificado; no selector durante login estándar. Identidad ambigua: contexto seguro o denegación.
6. Autorización en backend de cada recurso; IDs aleatorios, frontend guards y pertenencia al dominio nunca sustituyen el permiso.
7. Todos los perfiles: foto, datos, correo, cambio/recuperación de contraseña, MFA/passkeys según riesgo, sesiones revocables.
8. Timeline universal: cada cita/orden/servicio tiene eventos, responsables, observaciones, evidencias, aprobación versionada, notificación, entrega y resolución previa al cierre.
9. Archivos con cuotas, escaneo y preview fiel aislada; sin macros, enlaces públicos permanentes ni fuga por caché.
10. Pruebas BOLA/IDOR, inyección, XSS, CSRF, SSRF, suplantación host/tenant, recuperación y reuso de tokens, doble ejecución y concurrencia; riesgos P0/P1 bloquean cierre.

Estas reglas son criterios de implementación, NO resultados ejecutados ni promesa de riesgo cero.
