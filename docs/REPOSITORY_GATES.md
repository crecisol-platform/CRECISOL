# GitHub — protección deseada y estado real

Repo: solqaryn-platform/CRECISOL (Private). Owner: solqaryn-platform.

Estado leído antes de crear dev: main.protected=false. Se preparó un workflow mínimo con permisos read-only, pero **este archivo no configura reglas de GitHub por sí mismo**.

## Configuración requerida en GitHub → Settings → Rules → Rulesets
- main: impedir push directo, PR obligatorio, bloquear borrado/force push, exigir revisión independiente y resolución de conversaciones.
- exigir CI foundation y, cuando existan, pruebas Angular/.NET/MySQL; no fijar checks que aún no han ejecutado satisfactoriamente.
- dev: prohibir borrado y force push; integrar microtareas por PR cuando esté operacional.
- Requerir acciones seguras y vigilancia de secretos/dependencias según disponibilidad de la cuenta/plan. No afirmar que una opción está activa sin readback.
- Limitar quién puede editar configuración de repo. MFA para cuentas administrativas.
- Mantener despliegues deshabilitados: no workflows de producción, VPS, DNS ni conexiones Cloudflare.

**Acción humana de administración pendiente:** configurar ruleset en la UI si el conector no permite escritura de reglas, verificar readback en Settings y reportar si el plan no aplica sus restricciones a repositorios privados.
