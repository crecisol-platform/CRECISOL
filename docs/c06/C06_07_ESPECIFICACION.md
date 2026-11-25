# CRECISOL — C06-07 Privacidad y minimización de DTO

**CANDIDATA, NO CERTIFICADA.** Base e60c8a7642589f511b8beaa977f773524e963ece; repo original ID 1450414592, actual crecisol-platform/CRECISOL, dev. C06-06 certificada en alcance; C06 6/11; C06-08 bloqueada. Word autoridad blob fe7aaab60b603f1844e18666e2f99c0ea97e86e9, SHA-256 ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c; C06_WORD_LINE 15,17,18,28–33,41,61: minimizar, serialización selectiva, campos por autorización y retención documentada. Sin alterar Word, roles, rutas, MFA, UI, SOLQARYN o producción.

## 1. Alcance real
Las 75 operaciones dinámicas C05 + cuatro excepcionales C06-06 (79) reciben contrato positivo de salida exhaustivo y recursivo. Branding público, tres rutas neutrales de registro y ruta privada de harness ya tipados o constantes se preservan; no se activa la última en Program. No endpoints ni grants nuevos. C06_07_CONTRATOS_RESPUESTA.json es inventario verificable, no fuente de autorización.

La API proyecta después de autorización existente y antes de ambas salidas bearer/browser. No serializa objeto dinámico original ni diccionario arbitrario; construye objetos de campos permitidos, comprueba tipos, forma exacta y límites. Denegaciones descartan payload y metadatos. Propiedades nuevas no aprobadas provocan denegación; no se ejecutan getters de entidades arbitrarias. WebAuthn: selección explícita de campos estándar anidados, omisión de campos desconocidos y extensiones no usadas, rechazo de duplicados/tipos incorrectos. Se mantienen handles opacos exigidos por protocolo; no añade PII. El token explícito se conserva en recibos de sesión no browser; cookies HttpOnly y CSRF C05 intactos. Códigos de respaldo solo en mfa/verify cuando corresponde y confirmaciones/enrolamiento de operador; URI/semilla TOTP solo en operaciones expresas. Correo de solicitud de membresía limitado a hint enmascarado, no correo completo. Perfil propio, foto PNG canónica, eventos propios, sesión, estado de caso y referencia privada conservan autorización previa. No-store/private en módulo. Sin UI nueva.

## 2. Retención y disposición: política local
| Categoría | Vigencia/retención de laboratorio | Disposición |
|---|---|---|
| Payload recuperación/cambio de correo | 10 min C05, o consumo/revocación anterior | Rutina 022 borra lógicamente payload cifrado cerrado/caducado |
| Outbox sensible | Vigencia C05 según finalidad (10 min prueba; 24 h aviso; otros según expires_at_utc), o entrega anterior | Rutina 022 elimina payload cifrado al cierre/caducidad, conserva evidencia |
| Intento/confirmación de registro | TTL C05; disposición lógica sintética C05-02 | No afirmar que fixture sea purga de todas tablas operativas |
| Desafíos, invitaciones, pairing, WebAuthn, sesiones y casos | TTL/revocación/consumo de contrato existente; política de inactividad 5–120 min, default 30 | Inhabilitación lógica existente, no eliminación física |
| Perfil/foto/contacto/cuenta | Mientras se requiera para finalidad de cuenta y vínculo; foto se elimina lógicamente por acción propia | No ampliar acceso ni fijar plazo legal por inferencia |
| Auditoría, consentimiento y trazabilidad | Conservar durante desarrollo como evidencia; no alterar registros inmutables | Retención productiva, derechos, legal hold, backup/réplica y borrado físico requieren política jurídica/operativa aprobada antes de producción |

Procedimiento SQL SECURITY DEFINER local, invocado expresamente por rol offline EXECUTE-only. Un reloj UTC, transacción, lote 1..100 por tabla (hasta 200 filas totales), sin selectores de browser ni tenant arbitrario, respuesta ACK sin conteos/PII. Solo payloads de recuperación consumidos/expirados y outbox entregados/expirados en esquema operacional, nunca payload vivo, datos de cuenta ni bitácora. Conserva claves/FK/digests y estados. Disposición lógica no garantiza borrado físico InnoDB/binlogs/backups. Sin programador, seed, acceso de runtime, producción ni despliegue. Otros datos no se declaran eliminados.

## 3. Cinco auditorías
A maestro/catálogo 79 y alcance exacto; B contratos .NET por operación, campos anidados, sobreexposición, inválidos, denegaciones, WebAuthn real y neutralidad; C arquitectura, .NET10/Angular22/Node24 y regresiones; D MySQL8.4 operacional, dos tenants, lotes, idempotencia, payloads vivos/estados conservados, privilegios SQL y rollback; E Chromium/Angular/Kestrel/MySQL8.4 y oráculo independiente de respuestas HTTP bearer/browser en recorridos reales C05/C06-04/05/06. Cinco aprobadas por árbol/commit, 9.5/10 mínimo y acta antes de C06-08.

## 4. Reversión y límites
Revertir PR únicamente dev, retirar permiso del rol offline y rutina 022 en laboratorio; no restaurar payload ya dispuesto ni borrar evidencia. No certificar portal de derechos, operación productiva, borrado de backups ni Dedicated DB. main y SOLQARYN intactas.

## 5. Addendum de revalidación POST (25-11-2026)

La repetición POSTMERGE 45613536702 registró 4/5 trabajos aprobados; E (139202955345) falló después de 418 comprobaciones con una infracción en mfa/enroll. No es evidencia suficiente de una fuga ni de una respuesta correcta. La certificación PRE histórica se conserva; queda bloqueada la apertura de C06-08 hasta obtener evidencia real satisfactoria.

Candidata de corrección exclusiva de QA: inspección de texto HTTP sin convertir errores de lectura o JSON inválido en null; distinción entre captura incompleta y violación de contrato; comparación exacta de propiedades y cache-control; denegaciones sin campos; drenado de capturas antes de navegar y después de los dos enrollments; mensajes sin secretos. Se conserva el umbral de cobertura, los contratos del producto, el backend, frontend, MySQL y todas las regresiones. Diez pruebas locales con eventos simulados y un fixture mínimo mfa/enroll aprobadas; no equivalen a Chromium, Kestrel o MySQL reales. El workflow ejecutará el mismo archivo de pruebas contra el contrato completo del repositorio. El alcance alternativo de revalidación solo permite estos seis archivos y no modifica la lista histórica de veinte. Candidata NO CERTIFICADA; no fusionar ni abrir C06-08 sin validar de nuevo A/B/C/D/E y las regresiones aplicables sobre el árbol exacto.
