# C30 · Capacidades universales, permisos y rubros — microfases propuestas

> **BORRADOR, NO DESARROLLADO, NO CERTIFICADO.** Esta planificación deriva del índice aprobado y principios transversales. Antes de activarla, releer el capítulo íntegro del Documento Maestro Único verificado en `docs/MASTER_REFERENCE.md`, extraer IDs de requisitos y revisar este reparto por PR. No modifica el maestro.

**Entrada:** C29 con triple auditoría integral, acta y aceptación. **Riesgo clave:** Mismos módulos configurables por tenant/sector sin bypass RBAC.

## 10 microfases secuenciales

| Código | Entregable | Prueba positiva exigida | Bloqueo negativo exigido |
|---|---|---|---|
| C30-01 | Taxonomía de capacidades | IDs estables, dependencias y estados | No feature flags sin autorización |
| C30-02 | Matriz de rubro a capacidades | Comercio/servicio/membresía y mezclas trazadas | No replicar un backend por sector |
| C30-03 | Motor de entitlements por plan | Validación servidor e invalidación de caché | No activar por payload Angular |
| C30-04 | Resolución de permisos por actor | Rol, sucursal, pertenencia y propiedad | No heredar permiso de otro tenant |
| C30-05 | Configuración versionada por negocio | Borrador/publicado/rollback en datos tipados | No CSS/JS libre |
| C30-06 | Interfaz de módulos disponibles | Navegación y estados accesibles | No exponer acciones deshabilitadas API |
| C30-07 | Capacidades combinadas y casos mixtos | Fixtures con varios rubros por tenant | No colisión de estados |
| C30-08 | Pruebas de escalado de privilegios | Claims, roles, URL y cabeceras manipuladas | No acceso a módulo no contratado |
| C30-09 | Rendimiento y caché de decisiones | Latencias medidas, caches scoped | No reutilizar resultado de actor distinto |
| C30-10 | Auditorías integrales y acta C30 | A/B/C y aceptación | No abrir C31 sin catálogo seguro |

## Regla de avance y cierre

C30-XX se habilita únicamente tras el cierre probado de su microfase inmediatamente anterior (C30-01 requiere la aceptación integral de C29), con especificación individual en `docs/c30/C30_XX_ESPECIFICACION.md` creada **al comenzar**, trazabilidad real, tests positivos y negativos, auditorías A/B/C, cero P0/P1 sin resolver, revisión de diff/secretos, PR a dev, CI sobre último HEAD y tras merge, acta y rollback. Si no hay herramienta para ejecutar una prueba, marcar **NO EJECUTADA** y bloquear. Nadie puede adelantar otra microfase/capítulo por una CI parcial. No producción ni SOLQARYN.

El **capítulo C30** necesita además pruebas de regresión integral, auditorías A/B/C de capítulo, acta con aceptación expresa del propietario y certificación antes de habilitar C31. Cuando depende de aprobaciones externas o proveedor real y estos están prohibidos, el estado debe seguir **BLOQUEADO**, nunca certificado artificialmente.\n
## Ampliación comercial COM-001..COM-014 — dependencia transversal (10-11-2026)

**Solo planificación; NO IMPLEMENTADO / NO CERTIFICADO.** Requisitos derivados de `docs/ANALISIS_IMPACTO_MODELO_COMERCIAL_2026-11-10.md` y de una actualización del Word maestro **todavía pendiente de incorporación y comprobación de huellas en GitHub**; esta nota no sustituye el Word ni habilita C30.

- **COM:** COM-002, COM-003, COM-004, COM-012.
- **Microfases a revisar sin renumerar:** C30-01, C30-03, C30-04, C30-05, C30-06, C30-08, C30-09 y C30-10.
- **Aceptación futura:** Entitlements y límites de plan versionados y desacoplados de modalidad de dirección y plazo; parametrización SuperAdmin con autorización backend.
- **Negativos:** Cambiar slug no eleva permisos; cambiar tarifa no cambia accesos retrospectivos; caché de decisión no cruza tenant.
- **Pruebas definidas y NO ejecutadas:** COM-T01, COM-T07, COM-T08 y COM-T09.
- **Gate:** Word aprobado y verificable, capítulo precedente certificado, A/B/C, CI y acta propia. Sin datos productivos, despliegues ni pagos reales.
