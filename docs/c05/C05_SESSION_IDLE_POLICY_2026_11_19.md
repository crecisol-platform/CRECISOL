# CRECISOL — Política de vencimiento por inactividad C05-018

**Fecha:** 19 de noviembre de 2026.  
**Alcance:** C05, código y QA aislada, PR #172. **No equivale a aplicar la migración sobre el volumen persistente del propietario.**

## Decisión de producto

- La sesión autenticada de una empresa y la sesión de identidad de plataforma expiran tras **30 minutos de inactividad** por defecto, no por un plazo absoluto que obligue a entrar de nuevo a los 30 minutos pese a estar trabajando.
- La vigencia se renueva únicamente después de una petición autenticada validada en el servidor. Las interacciones reales del teclado/puntero pueden activar un keepalive limitado; el navegador no puede alterar la sesión por sí mismo ni extender un token expirado.
- El dato de configuración se guarda en MySQL 8.4: `c05_session_idle_policy` (fila única, `idle_minutes=30`, revisión optimista, administrador y fecha). Solo el backend establece los plazos y el cookie `Secure; HttpOnly; SameSite=Strict`.
- La consola global de operadores permite **consultar** el plazo. Únicamente el operador con permiso administrativo `can_manage_session_policy=1` puede **modificarlo entre 5 y 120 minutos**, previa revalidación del permiso, MFA, cuota por operación y CAS de revisión. Inicialmente solo `identity.one@example.test` recibe este permiso en el aprovisionamiento **sintético** C05; empresas y revisores no pueden gestionarlo.
- Los factores TOTP de seis dígitos continúan con pasos de 30 segundos. Los desafíos de enrolamiento, enlaces de correo y recuperación mantienen expiraciones más cortas propias; **no son sesiones** y no se amplían a 30 minutos.
- Si se reduce el intervalo, los tokens demasiado inactivos se revocan. Si posteriormente aumenta, no se reactivan credenciales revocadas. Durante la migración 018 los **tokens de sesión antiguos se invalidan** y sus usuarios deben entrar otra vez; no se modifican contraseñas, semillas TOTP, respaldos MFA, membresías ni acceso entre tenants.
- Angular muestra el plazo vigente, limpia secretos al expirar el plazo confirmado y no cierra remotamente la sesión de otra pestaña activa por un temporizador de una pestaña suspendida.

## Arquitectura y efectos

| Capa | Cambio C05 |
|---|---|
| Base de datos | Migración aditiva `018_c05_session_idle_policy.sql`: tabla de política con restricciones 5–120 y nueva columna de autorización con valor inicial FALSE |
| Backend .NET10 | `MySqlOperationalIdentityFlow.SessionPolicy.cs`: valida política con transacción, actualiza `last_seen_at_utc` y `expires_at_utc`, impide la reactivación de sesiones caducadas; garantiza el aislamiento de plataforma y de tenant |
| Transporte HTTP | Caducidad deslizante solo tras autorización; respuesta del servidor actualiza cookie HttpOnly y metadatos de vencimiento |
| Angular22 | Eventos humanos reales, keepalive limitado, reloj solo informativo y modal institucional para consulta/edición administrativa |
| Auditoría y privilegios | Operación `platform/session-policy/update` en bitácora inmutable de plataforma, MFA obligatorio, rango, cuota y revisión; rol MySQL con permisos exclusivamente sobre columnas necesarias |
| QA | Negativas MySQL para valores fuera de rango, privilegios, reejecución de 018; pruebas integradas Angular→API→MySQL de autorización, revisión CAS, vencimiento de 31 minutos y renovación activa |

## Instalación existente: sin migraciones implícitas

Una instalación nueva de QA aplica 001–018. La instalación existente **no se actualiza con `init` ni `start`**: ambos se niegan a abrir un ledger incompleto. El modo opcional `node qa/c05_local_persistent.mjs upgrade-session-policy` requiere **dos consentimientos explícitos separados**, API detenida, copia SQL comprobada mediante restauración aislada, material criptográfico original y ledger 001–017 íntegro, política TOTP-SHA1 ya activa y operadores sintéticos reaprovisionados. Si detecta migración 018 parcial, falla cerrado en vez de repetir DDL. No ejecutar en producción, VPS ni bases C01/C02. Las llaves externas requieren una **copia independiente cifrada y restaurada** antes de cualquier operación sobre el volumen real; el manifiesto de SQL por sí solo no guarda llaves.

## Condición de certificación

Aprobar CI real del SHA definitivo: compilación .NET10, Angular22/TypeScript, MySQL8.4, pruebas Chromium, seguridad multitenant/rol, migración 018, restauración y regresiones C01–C05, con cero P0/P1. La aprobación técnica de QA no certifica una prueba de 30 minutos en un dispositivo físico, una recuperación externa de claves ni la estética humana. Se conserva PR #172 como borrador y C06 bloqueado hasta cerrar las condiciones generales de C05.
