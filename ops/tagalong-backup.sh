#!/bin/sh
# Nightly backup (cron): every TagAlong database (checked after backing up),
# uploaded files (selfies, driver documents, encrypted recordings) and the
# settings file, as one archive per day in /var/backups/tagalong, kept 14 days.
# latest.tar always points at the newest; a PC downloads it as the off-server copy.
set -u
APP=/opt/tagalong
DEST=/var/backups/tagalong
KEEP_DAYS=14
DAY=$(date -u +%Y-%m-%d)
WORK=$DEST/.work-$DAY
VOL=/var/lib/docker/volumes/tagalong_sqlserver_data/_data
SQLCMD=/opt/mssql-tools18/bin/sqlcmd
LOG=/var/log/tagalong-backup.log

log() { echo "$(date -u '+%F %T') $*" >> "$LOG"; }
alert() { python3 "$APP/ops/tagalong-alert.py" "Backup FAILED $DAY" "$1" 2>> "$LOG" || log "alert email failed"; }

mkdir -p "$WORK" && chmod 750 "$DEST" "$WORK"
PW=$(grep '^SA_PASSWORD=' "$APP/.env" | cut -d= -f2-)
sql() { docker exec tagalong-sqlserver $SQLCMD -C -S localhost -U sa -P "$PW" -b -h -1 -W -Q "SET NOCOUNT ON; $1"; }

docker exec tagalong-sqlserver mkdir -p /var/opt/mssql/backup
failed=""
count=0
for db in $(sql "SELECT name FROM sys.databases WHERE name LIKE 'tagalong[_]%' ORDER BY name;" | tr -d '\r'); do
  f=/var/opt/mssql/backup/$db.bak
  if sql "BACKUP DATABASE [$db] TO DISK='$f' WITH INIT, CHECKSUM;" >/dev/null 2>&1 &&
     sql "RESTORE VERIFYONLY FROM DISK='$f' WITH CHECKSUM;" >/dev/null 2>&1; then
    gzip -c "$VOL/backup/$db.bak" > "$WORK/$db.bak.gz" && count=$((count + 1))
  else
    failed="$failed $db"
  fi
  rm -f "$VOL/backup/$db.bak"
done

tar czf "$WORK/user_uploads.tgz" -C /var/lib/docker/volumes/tagalong_user_uploads/_data . 2>> "$LOG" || failed="$failed uploads"
cp -p "$APP/.env" "$WORK/env"

OUT=$DEST/tagalong-$DAY.tar
tar cf "$OUT.part" -C "$WORK" . && mv "$OUT.part" "$OUT"
rm -rf "$WORK"
chgrp tagbackup "$OUT" 2>/dev/null && chmod 640 "$OUT"
ln -sfn "$OUT" "$DEST/latest.tar"

# Old ones go
find "$DEST" -maxdepth 1 -name 'tagalong-*.tar' -mtime +$KEEP_DAYS -delete

size=$(du -h "$OUT" | cut -f1)
if [ -n "$failed" ] || [ "$count" -eq 0 ]; then
  log "FAILED:$failed ($count databases ok, $size)"
  alert "Last night's backup had problems:$failed
Databases backed up: $count. File: $OUT ($size). Log: $LOG"
  exit 1
fi
log "ok: $count databases + uploads + settings, $size → $OUT"
