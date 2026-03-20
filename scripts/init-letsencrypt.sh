#!/bin/bash
# Run this ONCE on the VPS before starting the full stack.
# It provisions the Let's Encrypt SSL certificate for www.tlimc.net
set -e

DOMAIN="www.tlimc.net"
EXTRA_DOMAIN="tlimc.net"
EMAIL="info@tlimc.net"

CERT_PATH="./nginx/certbot/conf/live/$DOMAIN"

if [ -d "$CERT_PATH" ]; then
  echo "Certificate already exists at $CERT_PATH — skipping."
  exit 0
fi

# Create required directories
mkdir -p ./nginx/certbot/conf ./nginx/certbot/www

# Download recommended TLS options from certbot
if [ ! -f "./nginx/certbot/conf/options-ssl-nginx.conf" ]; then
  echo "Downloading recommended TLS parameters..."
  curl -s https://raw.githubusercontent.com/certbot/certbot/master/certbot-nginx/certbot_nginx/_internal/tls_configs/options-ssl-nginx.conf \
    -o ./nginx/certbot/conf/options-ssl-nginx.conf
  openssl dhparam -out ./nginx/certbot/conf/ssl-dhparams.pem 2048
fi

# Start a temporary nginx to serve the ACME challenge on port 80
echo "Starting temporary nginx for ACME challenge..."
docker run --rm -d \
  --name nginx-certbot-init \
  -p 80:80 \
  -v "$(pwd)/nginx/certbot/www:/var/www/certbot" \
  nginx:alpine \
  sh -c 'mkdir -p /var/www/certbot && nginx -g "daemon off;"'

sleep 3

# Request the certificate
echo "Requesting certificate for $DOMAIN and $EXTRA_DOMAIN..."
docker run --rm \
  -v "$(pwd)/nginx/certbot/conf:/etc/letsencrypt" \
  -v "$(pwd)/nginx/certbot/www:/var/www/certbot" \
  certbot/certbot certonly \
    --webroot \
    --webroot-path /var/www/certbot \
    --email "$EMAIL" \
    --agree-tos \
    --no-eff-email \
    -d "$DOMAIN" \
    -d "$EXTRA_DOMAIN"

# Stop temporary nginx
docker stop nginx-certbot-init

echo ""
echo "✓ Certificate issued successfully!"
echo "  Now start the full stack:"
echo "    docker compose -f docker-compose.production.yml --env-file .env up -d"
