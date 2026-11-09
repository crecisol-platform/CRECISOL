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

C30-XX se habilita solo tras C30-10 anterior cuando aplique, con especificación individual en `docs/c30/C30_XX_ESPECIFICACION.md` creada **al comenzar**, trazabilidad real, tests positivos y negativos, auditorías A/B/C, cero P0/P1 sin resolver, revisión de diff/secretos, PR a dev, CI sobre último HEAD y tras merge, acta y rollback. Si no hay herramienta para ejecutar una prueba, marcar **NO EJECUTADA** y bloquear. Nadie puede adelantar otra microfase/capítulo por una CI parcial. No producción ni SOLQARYN.

El **capítulo C30** necesita además pruebas de regresión integral, auditorías A/B/C de capítulo, acta aceptada y certificación expresa antes de habilitar C31. Cuando depende de aprobaciones externas o proveedor real y estos están prohibidos, el estado debe seguir **BLOQUEADO**, nunca certificado artificialmente.
