#!/bin/bash
# Run this ONCE on the VPS before starting the full stack.
# It provisions the Let's Encrypt SSL certificate for www.tlimc.net
set -e

DOMAIN="www.tlimc.net"
EXTRA_DOMAIN="tlimc.net"
EMAIL="info@tlimc.net"

CERT_PATH="/etc/letsencrypt/live/$DOMAIN"

if [ -d "$CERT_PATH" ]; then
  echo "Certificate already exists at $CERT_PATH — skipping."
  exit 0
fi

# Install certbot if not present
if ! command -v certbot &>/dev/null; then
  echo "Installing certbot..."
  apt-get update -qq
  apt-get install -y certbot python3-certbot-nginx
fi

# Request the certificate using the existing Nginx
echo "Requesting certificate for $DOMAIN and $EXTRA_DOMAIN..."
certbot certonly \
  --nginx \
  --non-interactive \
  --agree-tos \
  --email "$EMAIL" \
  -d "$DOMAIN" \
  -d "$EXTRA_DOMAIN"

# Copy certs to the location our Docker nginx expects
mkdir -p ./nginx/certbot/conf/live/$DOMAIN
cp /etc/letsencrypt/live/$DOMAIN/fullchain.pem ./nginx/certbot/conf/live/$DOMAIN/
cp /etc/letsencrypt/live/$DOMAIN/privkey.pem   ./nginx/certbot/conf/live/$DOMAIN/

# Copy TLS options files
cp /etc/letsencrypt/options-ssl-nginx.conf ./nginx/certbot/conf/ 2>/dev/null || \
  curl -s https://raw.githubusercontent.com/certbot/certbot/master/certbot-nginx/certbot_nginx/_internal/tls_configs/options-ssl-nginx.conf \
    -o ./nginx/certbot/conf/options-ssl-nginx.conf
cp /etc/letsencrypt/ssl-dhparams.pem ./nginx/certbot/conf/ 2>/dev/null || \
  openssl dhparam -out ./nginx/certbot/conf/ssl-dhparams.pem 2048

echo ""
echo "✓ Certificate issued and copied successfully!"
echo "  Now start the full stack:"
echo "    docker compose -f docker-compose.production.yml --env-file .env up -d --build"
