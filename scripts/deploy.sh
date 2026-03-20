#!/bin/bash
# TagAlong VPS Deployment Script
# Run this on your Hostinger VPS as root or a user with sudo + docker access
set -e

APP_DIR="/opt/tagalong"
REPO_URL="https://github.com/YOUR_USERNAME/YOUR_REPO.git"   # ← update this

# ── 1. Install Docker (skip if already installed) ─────────
if ! command -v docker &>/dev/null; then
  echo "Installing Docker..."
  curl -fsSL https://get.docker.com | sh
  systemctl enable docker
  systemctl start docker
fi

if ! command -v docker-compose &>/dev/null && ! docker compose version &>/dev/null; then
  echo "Installing Docker Compose plugin..."
  apt-get install -y docker-compose-plugin
fi

# ── 2. Clone or update the repo ───────────────────────────
if [ -d "$APP_DIR/.git" ]; then
  echo "Updating existing repo..."
  cd "$APP_DIR"
  git pull
else
  echo "Cloning repo..."
  git clone "$REPO_URL" "$APP_DIR"
  cd "$APP_DIR"
fi

# ── 3. Ensure .env exists ─────────────────────────────────
if [ ! -f "$APP_DIR/.env" ]; then
  echo ""
  echo "ERROR: .env file not found at $APP_DIR/.env"
  echo "Copy .env.production.example to .env and fill in your values:"
  echo "  cp .env.production.example .env && nano .env"
  exit 1
fi

# ── 4. Open firewall ports ────────────────────────────────
if command -v ufw &>/dev/null; then
  ufw allow 80/tcp
  ufw allow 443/tcp
  echo "Firewall: ports 80 and 443 opened"
fi

# ── 5. Build and start ────────────────────────────────────
echo "Building and starting containers..."
docker compose -f docker-compose.production.yml --env-file .env up -d --build

echo ""
echo "✓ Deployment complete"
echo "  Gateway: http://$(curl -s ifconfig.me)"
echo "  Logs:    docker compose -f docker-compose.production.yml logs -f"
echo "  Status:  docker compose -f docker-compose.production.yml ps"
