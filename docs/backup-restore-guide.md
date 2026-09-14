# PostgreSQL Backup & Disaster Recovery Guide

**Target System:** Physical Instagram Follower Counter Database  
**Database Engine:** PostgreSQL 17 / 16  
**RPO (Recovery Point Objective):** 24 Hours (Daily Backups) / Continuous WAL archiving for high-tier  
**RTO (Recovery Time Objective):** < 30 Minutes

---

## 1. Automated Daily Backup Strategy

We implement a daily dump script using `pg_dump` with custom directory format compression (`-Fc`), timestamping, and symmetric GPG encryption.

### Backup Retention Schedule:
- **Daily:** Retain for 7 days
- **Weekly:** Retain 4 weekly backups (Sunday snapshots)
- **Monthly:** Retain 3 monthly backups (1st of the month)

### Backup Script (`/opt/follower-counter/backup.sh`):
```bash
#!/bin/bash
set -euo pipefail

BACKUP_DIR="/var/backups/postgres"
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
DB_NAME="follower_counter_db"
DB_USER="follower_user"
DUMP_FILE="${BACKUP_DIR}/${DB_NAME}_${TIMESTAMP}.dump"
GPG_RECIPIENT="ops@counter.example.com"

mkdir -p "${BACKUP_DIR}"

# 1. Create compressed custom-format pg_dump
docker exec -t follower-postgres pg_dump -U "${DB_USER}" -d "${DB_NAME}" -Fc > "${DUMP_FILE}"

# 2. Encrypt using GPG
gpg --yes --encrypt --recipient "${GPG_RECIPIENT}" "${DUMP_FILE}"
rm -f "${DUMP_FILE}" # Remove unencrypted raw dump

echo "[$(date)] Backup completed: ${DUMP_FILE}.gpg"

# 3. Retention pruning: Remove daily backups older than 7 days
find "${BACKUP_DIR}" -name "${DB_NAME}_*.dump.gpg" -type f -mtime +7 -delete
```

---

## 2. Off-Site Replication

Never store backups solely on the same physical VPS as the running database. Configure an S3-compatible remote sync (e.g. AWS S3 Glacier, Cloudflare R2, or Wasabi):
```bash
rclone copy /var/backups/postgres remote-s3:counter-backups/daily/
```

---

## 3. Disaster Recovery / Restore Procedure

When recovering from a hardware failure or data corruption:

### Step 1: Provision Clean PostgreSQL Instance
```bash
docker compose up -d postgres
```

### Step 2: Decrypt the Selected Backup Snapshot
```bash
gpg --decrypt /var/backups/postgres/follower_counter_db_20260914_030000.dump.gpg > /tmp/restore.dump
```

### Step 3: Terminate Existing Connections & Restore
```bash
# Terminate existing connections if database is active
docker exec -i follower-postgres psql -U postgres -c "
SELECT pg_terminate_backend(pid) FROM pg_stat_activity 
WHERE datname = 'follower_counter_db' AND pid <> pg_backend_pid();"

# Drop and recreate database
docker exec -i follower-postgres psql -U postgres -c "DROP DATABASE IF EXISTS follower_counter_db;"
docker exec -i follower-postgres psql -U postgres -c "CREATE DATABASE follower_counter_db OWNER follower_user;"

# Restore using pg_restore
docker exec -i follower-postgres pg_restore -U follower_user -d follower_counter_db --clean --if-exists /tmp/restore.dump

# Securely wipe decrypted dump
shred -u /tmp/restore.dump
```

### Step 4: Validate Restored Database
```bash
docker exec -i follower-postgres psql -U follower_user -d follower_counter_db -c "
SELECT count(*) AS total_users FROM \"AspNetUsers\";
SELECT count(*) AS total_devices FROM \"Devices\";
SELECT count(*) AS total_instagram_accounts FROM \"InstagramAccounts\";"
```

### Step 5: Start API & Background Workers
```bash
docker compose up -d api worker
```
Verify `/health/ready` responds with HTTP 200 OK.
