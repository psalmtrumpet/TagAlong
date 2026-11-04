#!/bin/sh
# Every 5 minutes (cron): is TagAlong up? Emails info@tagalong.delivery when
# something breaks, again every hour while it stays broken, and when it's fixed.
# (Catches crashed services, a full disk or a missed backup. If the whole
# server is down this can't send anything: an outside monitor covers that.)
set -u
APP=/opt/tagalong
STATE=/var/lib/tagalong-watchdog
mkdir -p "$STATE"
problems=""
add() { problems="$problems
- $1"; }

# The API answers (a made-up ride: 404 means gateway + messaging are working)
c=$(curl -s -o /dev/null -m 15 -w '%{http_code}' https://www.tlimc.net/api/public/tracking/00000000-0000-0000-0000-000000000000)
[ "$c" = 404 ] || add "API not answering properly (got $c)"
# Sign-in service (a wrong login is refused with 401)
c=$(curl -s -o /dev/null -m 15 -w '%{http_code}' -X POST https://www.tlimc.net/api/auth/login \
    -H 'Content-Type: application/json' -d '{"email":"watchdog@tagalong.delivery","password":"x"}')
[ "$c" = 401 ] || add "Sign-in not answering properly (got $c)"
c=$(curl -s -o /dev/null -m 15 -w '%{http_code}' https://tagalong.delivery/)
[ "$c" = 200 ] || add "Website tagalong.delivery not answering (got $c)"

# Every TagAlong container running, none unhealthy
for name in $(cd "$APP" && docker compose -f docker-compose.production.yml config --format json 2>/dev/null |
              python3 -c "import sys,json;print('\n'.join(s.get('container_name','') for s in json.load(sys.stdin)['services'].values()))"); do
  [ -z "$name" ] && continue
  st=$(docker inspect -f '{{.State.Status}} {{if .State.Health}}{{.State.Health.Status}}{{end}}' "$name" 2>/dev/null || echo "missing")
  case "$st" in
    "running "|"running healthy"|"running starting") ;;
    *) add "$name: $st" ;;
  esac
done

used=$(df --output=pcent / | tail -1 | tr -dc '0-9')
[ "$used" -ge 85 ] && add "Disk ${used}% full"
mem=$(free | awk '/Mem:/ {printf "%d", ($3/$2)*100}')
[ "$mem" -ge 92 ] && add "Memory ${mem}% used"

# Last night's backup is there
latest=$(readlink -f /var/backups/tagalong/latest.tar 2>/dev/null)
if [ -z "$latest" ] || [ $(( $(date +%s) - $(stat -c %Y "$latest" 2>/dev/null || echo 0) )) -gt 108000 ]; then
  add "No backup in the last 30 hours"
fi

now=$(date +%s)
was=$(cat "$STATE/problems" 2>/dev/null || true)
last=$(cat "$STATE/last-alert" 2>/dev/null || echo 0)
if [ -n "$problems" ]; then
  if [ "$problems" != "$was" ] || [ $((now - last)) -ge 3600 ]; then
    python3 "$APP/ops/tagalong-alert.py" "Something is wrong" "Checked $(date -u '+%F %H:%M') UTC:$problems

Logs: docker compose -f docker-compose.production.yml logs --tail 100 <service>" && echo "$now" > "$STATE/last-alert"
  fi
  printf '%s' "$problems" > "$STATE/problems"
elif [ -n "$was" ]; then
  python3 "$APP/ops/tagalong-alert.py" "All working again" "Checked $(date -u '+%F %H:%M') UTC: everything answers again.
Earlier:$was"
  rm -f "$STATE/problems" "$STATE/last-alert"
fi
