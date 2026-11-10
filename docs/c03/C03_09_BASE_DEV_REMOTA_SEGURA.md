# CRECISOL — C03-09 | Base DEV remota: política de prueba segura

**Estado:** preparación documental y verificador opcional de **solo lectura**, sin conexión realizada, sin despliegue y sin certificación. Repositorio autorizado: `solqaryn-platform/CRECISOL`; trabajo limitado a C03-09.

## Información comunicada por el propietario, aún no verificada

El propietario informó de una base de desarrollo llamada `crecisol_dev`, alojada en un servidor remoto, conectividad TCP 3306, cuenta de aplicación de desarrollo y `SSL REQUIRED`. Indicó que la base está vacía. **No se recibió contraseña real ni certificado de confianza**, y no se verificaron versión MySQL, contenido, permisos, TLS, aislamiento del entorno o conectividad. No guardamos host, nombre de usuario, contraseñas ni certificados en el repositorio.

**Importante:** una base de desarrollo alojada en infraestructura de producción **continúa siendo un servicio remoto persistente**. Que esté vacía no autoriza pruebas ilimitadas, borrados generales, ejecución de migraciones irreversibles, cargas de estrés ni acceso desde redes no autorizadas.

## Separar dos clases de pruebas

1. **Pruebas con escritura, errores inducidos, schema y rollback:** MySQL 8.4 **desechable, aislado** mediante `database/tests/c03_07_outbox_mysql_smoke.sh`. No apuntar estos scripts a ningún host remoto. Se permiten fixtures sintéticos y tablas temporales según la prueba, no credenciales reales.
2. **Verificación externa del servicio CRECISOL DEV:** únicamente consulta de metadatos `SELECT VERSION(), DATABASE()` y `SHOW SESSION STATUS LIKE 'Ssl_cipher'`. Sin crear, insertar, actualizar, borrar, migrar, cargar fixtures ni ejecutar benchmark de carga. Implementación independiente en `qa/c03_09_dev_db_tls_probe.py`; **no se llama automáticamente desde el lanzador general**.

La prueba externa **no** demuestra compatibilidad de migraciones, aislamiento de tenants ni seguridad de la aplicación. Tampoco reemplaza MySQL 8.4 aislado en la auditoría C.

## Protección del transporte y de la identidad

MySQL distingue `--ssl-mode=REQUIRED` (cifrado obligatorio) y `--ssl-mode=VERIFY_IDENTITY` (cifrado, CA de confianza y verificación del nombre del servidor). **REQUIRED por sí solo no comprueba la identidad del destino.** Para la primera conexión a través de una red remota, usar `VERIFY_IDENTITY` y la CA de confianza del servidor.

Si el cliente se conecta a una dirección IP, el certificado debe coincidir con la identidad utilizada, por ejemplo un SAN IP válido; cuando no coincida, obtener un nombre DNS/certificado correcto y consultar al administrador, **nunca** degradar a `REQUIRED` para evadir un fallo. No modificar aquí firewall, DNS, servidor, cuentas SQL ni TLS de una infraestructura remota.

Fuente técnica consultada: [MySQL 8.4 — Configuring Encrypted Connections](https://dev.mysql.com/doc/refman/8.4/en/using-encrypted-connections.html) y [mysql_config_editor](https://dev.mysql.com/doc/refman/8.4/en/mysql-config-editor.html).

## Preparación voluntaria en una PC de desarrollo, fuera de Git

Antes de efectuar conexión: confirmar por un canal seguro la identidad del servidor, un **certificado CA confiable** y un **usuario de solo lectura específico** (preferible a la cuenta de aplicación). Limitar el tráfico al origen autorizado mediante red privada/VPN o reglas IP adecuadas, bajo control del administrador. La contraseña se introduce **solo en el equipo local**, nunca en un chat, PR, workflow, comando con password en texto ni captura.

Desde WSL2, con cliente MySQL 8.4 instalado, el propietario podrá configurar manualmente su perfil. Los marcadores en mayúsculas deben sustituirse por valores confiables **sin guardarlos en el repo**:

```bash
mysql_config_editor set --login-path=crecisol_dev_readonly --host=HOST_DEV_VERIFICADO --port=3306 --user=USUARIO_LECTURA_DEV --password
chmod 600 "$HOME/.mylogin.cnf"

# Sin contacto con la base; solo plan:
python3 qa/c03_09_dev_db_tls_probe.py --plan
python3 -m unittest discover -s qa -p test_c03_09_dev_db_tls_probe.py -v

# Solo después de validar permisos, CA e identidad. Nunca se ejecuta automáticamente:
python3 qa/c03_09_dev_db_tls_probe.py --connect-readonly --confirm-dev-only --ca "$HOME/certificados/crecisol-dev-ca.pem"
```

El archivo `.mylogin.cnf` reside fuera del repositorio, contiene las credenciales ofuscadas por MySQL (no es una bóveda criptográfica) y debe protegerse con permisos de usuario. El certificado CA se entrega/verifica por un canal independiente, no por el servidor en una conexión de confianza aún no establecida. El script requiere CA en ruta absoluta, archivo PEM regular, fuera del checkout y no symlink.

El verificador no conecta en `--plan` ni `--self-test`. La conexión exige explícitamente `--connect-readonly --confirm-dev-only --ca ...` y usa `--no-defaults`, `--login-path`, `--protocol=TCP`, `--ssl-mode=VERIFY_IDENTITY`, `--local-infile=0` y SQL fijo. Si falta CA, CLI, identidad TLS, versión 8.4, base correcta o el cifrado, registra **bloqueo** sin volcar diagnóstico del servidor ni afirmar éxito. El informe `phase_certified=false` siempre.

## Matriz de tres auditorías para el acceso remoto

| Auditoría | Revisión válida ahora | Pendiente con equipo/acceso |
| --- | --- | --- |
| **A: trazabilidad** | Configuración documentada como entrada comunicada, no comprobada. Alcance de solo metadatos ligado a C03-09 | Contrastar el recurso, permisos, versión y hostname real con responsable del servidor |
| **B: seguridad** | 13 pruebas negativas/positivas offline del verificador: TLS estricto, CA, SQL fijo, no credenciales ni conexión implícita | Comprobar acceso mínimo real, CA/SAN confiables y firewall; negativas de permisos con usuario aislado |
| **C: compatibilidad/rendimiento** | Código validado por sintaxis y tests offline, pero no benchmark | Compilar Angular/.NET en entorno real, ejecutar MySQL 8.4 desechable y opcionalmente metadata remota sin escritura |

**Puerta:** C03-09 sigue **EN DESARROLLO / NO CERTIFICADA**. C03-10 sigue pendiente; C03 sin certificar; C04 bloqueado; C01, C02 y C03-01..08 no se alteran. No crear migraciones, servicios productivos, pagos o configuraciones VPS en este paso.
