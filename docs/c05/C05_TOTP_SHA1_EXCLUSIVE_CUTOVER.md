# C05 — corte exclusivo TOTP HMAC-SHA1 (mantenimiento local)

**Estado:** RAMA/PR EN AUDITORÍA, NO FUSIONAR, NO APLICAR SOBRE EL VOLUMEN PERSISTENTE.

## Decisión
Por instrucción explícita del propietario, se retira la verificación TOTP-SHA256,
sin modo dual ni downgrade; las cuentas locales de prueba pueden perder acceso
y deberán registrar un nuevo autenticador TOTP SHA1. Esta política no modifica
Argon2id, HMAC-SHA256 de tokens/backups, WebAuthn ni claves criptográficas.

## Componentes
- `TotpSha1Verifier`: RFC6238, secreto aleatorio de 20 bytes, 6 dígitos,
  período 30s, paso actual/anterior, contador de un solo uso.
- APIs enterprise/platform: sólo URI `otpauth://totp/...` con `algorithm=SHA1`.
  La semilla/URI se retorna exclusivamente en el enrolamiento protegido.
- Angular: QR generado en memoria, alternativa manual colapsada, limpieza al
  confirmar/cerrar/caducar. Google/Microsoft/Aegis son objetivos de prueba
  humana, no certificaciones de interoperabilidad física automática.
- C05-017: migración aditiva e irreversible en datos MFA. El estado 0 bloquea
  ejecución, el estado 1 se activa solo si desaparecieron factores antiguos;
  segunda ejecución intencionalmente rechazada. `down.sql` no puede recuperar
  secretos; existe sólo para limpiar esquema de pruebas descartables.
- MySQL: única nueva tabla `c05_totp_sha1_policy` con SELECT de mínima
  autoridad para API; ni C01/C02 ni migraciones históricas 001..016 cambian.

## Seguridad del corte local persistente: NO AUTOMÁTICO
1. Detener API C05 en `127.0.0.1:5187` sin parar C01/C02. No ejecutar jamás
   este procedimiento contra `crecisol_local`, VPS o una base remota.
2. Completar `backup` y `verify-backup` del PR #169 sobre C05 y guardar
   juntas la copia SQL y claves externas con permisos 0600. Comprobar restauración.
3. Verificar exactamente el esquema 001..016, nombres de contenedor y volumen
   y congelar nuevas sesiones. Asegurar un mecanismo offline autorizado para
   reaprovisionar los tres operadores `@example.test` ANTES de iniciar.
4. Con API detenida, aplicar `017_c05_totp_sha1_exclusive.sql` UNA SOLA VEZ
   con conexión administrativa local (nunca credenciales en stdout/PR/logs).
   Registrar checksum y versión en el ledger de migraciones del supervisor.
   El SQL inutiliza deliberadamente TODOS los códigos, semillas, sesiones y
   respaldos TOTP anteriores. Contraseñas, tenants, perfiles y passkeys
   permanecen.
5. Usar el aprovisionador offline `--isolated-existing-factor-bundle` sólo
   para operador sintético: elegir su ID original y mailbox por digest local,
   cifrar nueva semilla con ese ID, y actualizar bajo transacción el operador
   si está en `recovery_pending=1` con semilla vacía. Crear diez códigos
   de respaldo nuevos y dejar la cuenta activa; no devolver antiguo MFA.
   El script de aplicación SQL todavía requiere integración/QA con PR #169:
   **prohibido ejecutar el corte del volumen del propietario hasta que
   dicho procedimiento haya pasado pruebas reales.**
6. Resto de usuarios: reactivar por recuperación existente con 2 operadores
   independientes o passkey válido, nunca con contraseña sola ni bypass.
   Confirmar QR/código SHA1 y respaldo nuevo; invalidar factor anterior.
7. Reiniciar API/frontend, probar acceso real, cierre y reinicio total.
   No afirmar haber probado Google/Microsoft en teléfono si no lo hizo
   el propietario; documentar aceptación humana por interfaz.

## Verificación
Auditorías A/B/C de este incremento; D Chromium de vistas/modales con QR
decodificable, E integración real Angular22/.NET10/MySQL8.4, replay, cuotas,
WebAuthn, recuperación, sesiones, negativas y regresiones C01–C05.
`database/tests/c05_sha1_cutover_mysql.sh` sólo usa contenedor descartable.

## Reversión y protección
No existe rollback lógico de semillas destruidas. Para recuperar acceso
histórico sería imprescindible restaurar conjuntamente copia SQL anterior
y llaves cifradas en un entorno C05 temporal separado; no reintroducir
SHA256 en la aplicación definitiva. La base C01/C02, `dev`, `main`,
Documento Maestro, C06, SOLQARYN y producción permanecen fuera del alcance.
