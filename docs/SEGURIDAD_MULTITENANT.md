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

Estas reglas son criterios de implementación, NO resultados ejecutados ni promesa de riesgo cero.\n\n## Controles P0 comerciales propuestos (COM-001..COM-014)

**Planificación condicionada, sin código ni pruebas ejecutadas.** El Word canónico comercial y su nueva huella ya constan en la rama del PR #73; su adopción normativa queda pendiente de las puertas de CI, auditoría y fusión a dev. Al implementar C04/C08/C19/C20/C23/C26/C31/C33/C34/C36:

- La elección de slug, ruta, subdominio, dominio o tarifa **nunca** concede pertenencia ni privilegios. Host/claims/query falsos y cambios de caché/eventos deben fallar cerrados.
- SuperAdmin modifica precios/contratos SaaS solo con MFA/step-up, permiso y auditoría; no accede a datos privados sin grant excepcional. Propietarios solo consultan/solicitan cambios de su contrato.
- Un pago o renovación usa versión aceptada, idempotencia, firma/autenticación de evento y conciliación; duplicados o fallos no activan, cobran doble ni suspenden por error.
- Suspensión por mora es reversible y auditada; no equivale a eliminación. Retención, exportación y bajas legales deben conservar aislamiento A/B.
- Publicidad en sitios de empresas exige autorización comercial, slots permitidos, sanitización de medios/contenido, control de audiencia y no extracción de PII de tenants. SOLQARYN no concede ningún acceso técnico.
- Efectuar regresiones negativas `COM-T04`, `COM-T07`, `COM-T08`, `COM-T09`, `COM-T10`, `COM-T14`, `COM-T16`, `COM-T17` en su microfase autorizada. Casos definidos, NO ejecutados.
