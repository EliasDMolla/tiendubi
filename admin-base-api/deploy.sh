#!/usr/bin/env bash
set -Eeuo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="$(git -C "$SCRIPT_DIR" rev-parse --show-toplevel 2>/dev/null || printf '%s' "$SCRIPT_DIR")"
ENV_FILE="$SCRIPT_DIR/.env.production"
ENV_EXAMPLE="$SCRIPT_DIR/.env.production.vps.example"

get_env_value() {
  local key="$1"
  grep -E "^${key}=" "$ENV_FILE" | tail -n 1 | cut -d '=' -f 2- || true
}

set_env_value() {
  local key="$1"
  local value="$2"
  local temp_file
  temp_file="$(mktemp)"

  awk -v key="$key" -v value="$value" '
    BEGIN { found = 0 }
    index($0, key "=") == 1 { print key "=" value; found = 1; next }
    { print }
    END { if (!found) print key "=" value }
  ' "$ENV_FILE" > "$temp_file"

  mv "$temp_file" "$ENV_FILE"
  chmod 600 "$ENV_FILE"
}

is_placeholder() {
  printf '%s' "$1" | grep -Eqi '(^$|REPLACE|CHANGE_ME|CAMBIAR|YOUR_|example\.com|tudominio\.com|PASSWORD_FUERTE|capturar|ordenapp)'
}

require_command() {
  command -v "$1" >/dev/null 2>&1 || { echo "ERROR: Falta el comando requerido: $1"; exit 1; }
}

generate_secret() {
  if command -v openssl >/dev/null 2>&1; then
    openssl rand -base64 48 | tr -d '\n'
  else
    od -An -N48 -tx1 /dev/urandom | tr -d ' \n'
  fi
}

require_command git
require_command docker
require_command curl

echo "[1/8] Preparando el repositorio..."
if [ "${SKIP_GIT_PULL:-false}" != "true" ]; then
  git -C "$REPO_DIR" pull "${DEPLOY_REMOTE:-origin}" "${DEPLOY_BRANCH:-main}"
fi

echo "[2/8] Validando configuracion..."
if [ ! -f "$ENV_FILE" ]; then
  cp "$ENV_EXAMPLE" "$ENV_FILE"
  chmod 600 "$ENV_FILE"
  echo "Se creo $ENV_FILE desde el ejemplo. Completa sus valores y ejecuta nuevamente bash deploy.sh."
  exit 1
fi
chmod 600 "$ENV_FILE"

APP_NAME="${APP_NAME:-$(get_env_value DEPLOY_APP_NAME)}"
NETWORK_NAME="${NETWORK_NAME:-$(get_env_value DEPLOY_NETWORK_NAME)}"
HOST_PORT="${HOST_PORT:-$(get_env_value DEPLOY_HOST_PORT)}"
API_DOMAIN="${API_DOMAIN:-$(get_env_value DEPLOY_API_DOMAIN)}"
API_URL="${API_URL:-$(get_env_value DEPLOY_API_URL)}"
FRONTEND_URL="${FRONTEND_URL:-$(get_env_value AppSettings__FrontendUrl)}"
ALLOWED_ORIGINS="${FRONTEND_ALLOWED_ORIGINS:-$(get_env_value Cors__AllowedOrigins)}"
DATABASE_MODE="${DATABASE_MODE:-$(get_env_value DEPLOY_DATABASE_MODE)}"
CONFIGURE_NGINX="${CONFIGURE_NGINX:-$(get_env_value DEPLOY_CONFIGURE_NGINX)}"
ENABLE_HTTPS="${ENABLE_HTTPS:-$(get_env_value DEPLOY_ENABLE_HTTPS)}"
CERTBOT_EMAIL="${CERTBOT_EMAIL:-$(get_env_value DEPLOY_CERTBOT_EMAIL)}"
NGINX_SITE_NAME="${NGINX_SITE_NAME:-$(get_env_value DEPLOY_NGINX_SITE_NAME)}"

if [ -z "$CERTBOT_EMAIL" ]; then
  CERTBOT_EMAIL="$(get_env_value OwnerSecurity__OwnerEmail)"
fi

APP_NAME="${APP_NAME:-tiendubi-webapi}"
NETWORK_NAME="${NETWORK_NAME:-tiendubi-net}"
HOST_PORT="${HOST_PORT:-5002}"
API_DOMAIN="${API_DOMAIN:-api.tiendubi.com}"
API_URL="${API_URL:-https://api.tiendubi.com}"
FRONTEND_URL="${FRONTEND_URL:-https://tiendubi.com}"
ALLOWED_ORIGINS="${ALLOWED_ORIGINS:-$FRONTEND_URL}"
DATABASE_MODE="${DATABASE_MODE:-external}"
CONFIGURE_NGINX="${CONFIGURE_NGINX:-true}"
ENABLE_HTTPS="${ENABLE_HTTPS:-true}"
NGINX_SITE_NAME="${NGINX_SITE_NAME:-tiendubi-api}"

if [[ ! "$APP_NAME" =~ ^[a-zA-Z0-9._-]+$ ]] || [[ ! "$NGINX_SITE_NAME" =~ ^[a-zA-Z0-9._-]+$ ]]; then
  echo "ERROR: Los nombres de app y sitio Nginx contienen caracteres no permitidos."
  exit 1
fi
if [[ ! "$HOST_PORT" =~ ^[0-9]+$ ]] || [ "$HOST_PORT" -lt 1 ] || [ "$HOST_PORT" -gt 65535 ]; then
  echo "ERROR: DEPLOY_HOST_PORT debe ser un puerto valido."
  exit 1
fi
if [[ ! "$API_DOMAIN" =~ ^[a-zA-Z0-9.-]+$ ]]; then
  echo "ERROR: DEPLOY_API_DOMAIN no tiene un formato valido."
  exit 1
fi
if is_placeholder "$API_URL" || is_placeholder "$FRONTEND_URL"; then
  echo "ERROR: Configura DEPLOY_API_URL y AppSettings__FrontendUrl."
  exit 1
fi
if [ "$DATABASE_MODE" != "external" ] && [ "$DATABASE_MODE" != "docker" ]; then
  echo "ERROR: DEPLOY_DATABASE_MODE debe ser external o docker."
  exit 1
fi
if [ "$CONFIGURE_NGINX" = "true" ] && [ "$ENABLE_HTTPS" = "true" ]; then
  if is_placeholder "$CERTBOT_EMAIL" || [[ ! "$CERTBOT_EMAIL" =~ ^[^[:space:]@]+@[^[:space:]@]+\.[^[:space:]@]+$ ]]; then
    echo "ERROR: Configura DEPLOY_CERTBOT_EMAIL para solicitar HTTPS."
    exit 1
  fi
fi

JWT_SECRET="$(get_env_value Jwt__Secret)"
if is_placeholder "$JWT_SECRET" || [ "${#JWT_SECRET}" -lt 32 ]; then
  JWT_SECRET="$(generate_secret)"
  set_env_value "Jwt__Secret" "$JWT_SECRET"
  echo "Se genero un Jwt__Secret seguro."
fi

set_env_value "ASPNETCORE_ENVIRONMENT" "Production"
set_env_value "ASPNETCORE_URLS" "http://+:8081"
set_env_value "PORT" "8081"
set_env_value "DATA_PROTECTION_KEYS_PATH" "/app/keys"
set_env_value "AppSettings__FrontendUrl" "$FRONTEND_URL"
set_env_value "Cors__AllowedOrigins" "$ALLOWED_ORIGINS"
set_env_value "Features__SeedDevelopmentAdmin" "false"

echo "[3/8] Preparando red y base de datos..."
docker network inspect "$NETWORK_NAME" >/dev/null 2>&1 || docker network create "$NETWORK_NAME" >/dev/null

if [ "$DATABASE_MODE" = "docker" ]; then
  POSTGRES_NAME="${POSTGRES_NAME:-$(get_env_value POSTGRES_CONTAINER)}"
  POSTGRES_DB="${POSTGRES_DB:-$(get_env_value POSTGRES_DB)}"
  POSTGRES_USER="${POSTGRES_USER:-$(get_env_value POSTGRES_USER)}"
  POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-$(get_env_value POSTGRES_PASSWORD)}"
  POSTGRES_ADMIN_USER="${POSTGRES_ADMIN_USER:-$(get_env_value POSTGRES_ADMIN_USER)}"
  POSTGRES_ADMIN_DB="${POSTGRES_ADMIN_DB:-$(get_env_value POSTGRES_ADMIN_DB)}"
  POSTGRES_AUTO_CREATE="${POSTGRES_AUTO_CREATE:-$(get_env_value POSTGRES_AUTO_CREATE)}"
  POSTGRES_NAME="${POSTGRES_NAME:-postgres}"
  POSTGRES_DB="${POSTGRES_DB:-tiendubi}"
  POSTGRES_USER="${POSTGRES_USER:-tiendubi}"
  POSTGRES_ADMIN_USER="${POSTGRES_ADMIN_USER:-postgres}"
  POSTGRES_ADMIN_DB="${POSTGRES_ADMIN_DB:-postgres}"
  POSTGRES_AUTO_CREATE="${POSTGRES_AUTO_CREATE:-true}"

  if is_placeholder "$POSTGRES_PASSWORD" || [[ "$POSTGRES_PASSWORD" == *";"* ]]; then
    echo "ERROR: Configura un POSTGRES_PASSWORD fuerte y sin punto y coma."
    exit 1
  fi
  if ! docker ps -a --format '{{.Names}}' | grep -qx "$POSTGRES_NAME"; then
    echo "ERROR: No existe el contenedor PostgreSQL: $POSTGRES_NAME"
    exit 1
  fi

  docker start "$POSTGRES_NAME" >/dev/null
  docker network connect "$NETWORK_NAME" "$POSTGRES_NAME" >/dev/null 2>&1 || true
  for attempt in {1..30}; do
    docker exec "$POSTGRES_NAME" pg_isready -U "$POSTGRES_ADMIN_USER" -d "$POSTGRES_ADMIN_DB" >/dev/null 2>&1 && break
    if [ "$attempt" -eq 30 ]; then echo "ERROR: PostgreSQL no quedo listo."; exit 1; fi
    sleep 2
  done

  if [ "$POSTGRES_AUTO_CREATE" = "true" ]; then
    docker exec -i \
      -e "APP_DB_NAME=$POSTGRES_DB" -e "APP_DB_USER=$POSTGRES_USER" \
      -e "APP_DB_PASSWORD=$POSTGRES_PASSWORD" -e "PG_ADMIN_USER=$POSTGRES_ADMIN_USER" \
      -e "PG_ADMIN_DB=$POSTGRES_ADMIN_DB" "$POSTGRES_NAME" sh -ceu '
        exec psql --set ON_ERROR_STOP=1 --username "$PG_ADMIN_USER" --dbname "$PG_ADMIN_DB" \
          --set app_db="$APP_DB_NAME" --set app_user="$APP_DB_USER" --set app_password="$APP_DB_PASSWORD"
      ' <<'SQL'
SELECT format('CREATE ROLE %I LOGIN', :'app_user')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'app_user')
\gexec
SELECT format('ALTER ROLE %I WITH LOGIN PASSWORD %L', :'app_user', :'app_password')
\gexec
SELECT format('CREATE DATABASE %I OWNER %I', :'app_db', :'app_user')
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = :'app_db')
\gexec
SQL
  fi
  set_env_value "ConnectionStrings__PostgresConnection" "Host=${POSTGRES_NAME};Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
else
  CONNECTION_STRING="$(get_env_value ConnectionStrings__PostgresConnection)"
  if is_placeholder "$CONNECTION_STRING"; then
    echo "ERROR: Configura ConnectionStrings__PostgresConnection para la base externa."
    exit 1
  fi
fi

echo "[4/8] Construyendo e iniciando la API..."
cd "$SCRIPT_DIR"
docker compose --env-file "$ENV_FILE" build --pull
docker compose --env-file "$ENV_FILE" up -d --remove-orphans

echo "[5/8] Esperando el health check..."
for attempt in {1..45}; do
  if curl --fail --silent "http://127.0.0.1:${HOST_PORT}/api/health" >/dev/null; then
    break
  fi
  if [ "$attempt" -eq 45 ]; then
    echo "ERROR: La API no respondio correctamente."
    docker logs --tail 150 "$APP_NAME" || true
    exit 1
  fi
  sleep 2
done

if [ "$CONFIGURE_NGINX" = "true" ]; then
  echo "[6/8] Configurando Nginx..."
  require_command sudo
  require_command nginx
  [ "$ENABLE_HTTPS" != "true" ] || require_command certbot
  sudo -v

  NGINX_TEMPLATE="$SCRIPT_DIR/nginx/tiendubi-api.conf.example"
  NGINX_SITE_PATH="/etc/nginx/sites-available/$NGINX_SITE_NAME"
  NGINX_ENABLED_PATH="/etc/nginx/sites-enabled/$NGINX_SITE_NAME"

  if sudo test -f "$NGINX_SITE_PATH"; then
    if ! sudo grep -Fq "server_name ${API_DOMAIN};" "$NGINX_SITE_PATH" || \
       ! sudo grep -Fq "proxy_pass http://127.0.0.1:${HOST_PORT};" "$NGINX_SITE_PATH"; then
      echo "ERROR: $NGINX_SITE_PATH ya existe con otro dominio o puerto."
      exit 1
    fi
  else
    sed -e "s/api\.tiendubi\.com/${API_DOMAIN}/g" \
        -e "s/127\.0\.0\.1:5002/127.0.0.1:${HOST_PORT}/g" \
        "$NGINX_TEMPLATE" | sudo tee "$NGINX_SITE_PATH" >/dev/null
  fi

  sudo ln -sfn "$NGINX_SITE_PATH" "$NGINX_ENABLED_PATH"
  sudo nginx -t
  sudo systemctl reload nginx

  if [ "$ENABLE_HTTPS" = "true" ] && ! sudo grep -Eq 'listen[[:space:]]+443([^;]*[[:space:]])?ssl' "$NGINX_SITE_PATH"; then
    sudo certbot --nginx --non-interactive --agree-tos --redirect --keep-until-expiring \
      --email "$CERTBOT_EMAIL" -d "$API_DOMAIN"
    sudo nginx -t
    sudo systemctl reload nginx
  fi
else
  echo "[6/8] Configuracion automatica de Nginx deshabilitada."
fi

echo "[7/8] Verificando el contenedor..."
docker compose --env-file "$ENV_FILE" ps

echo "[8/8] Deploy completado."
echo "API publica: $API_URL"
echo "Health:      $API_URL/api/health"
echo "Logs:        docker logs -f $APP_NAME"
