# ============================================================================
# CleanTenant — Docker Temizleme ve Yeniden Kurulum (PowerShell)
# ============================================================================
# Kullanım: .\cleanup.ps1
# ============================================================================

Write-Host "=== CleanTenant Docker Temizleme ===" -ForegroundColor Yellow

# 1. Mevcut container'ları durdur ve sil
Write-Host "1. Container'lar durduruluyor..." -ForegroundColor Cyan
docker-compose --env-file ../.env down -v 2>$null

# 2. CleanTenant volume'larını sil
Write-Host "2. Volume'lar siliniyor..." -ForegroundColor Cyan
docker volume ls --format "{{.Name}}" | Where-Object { $_ -like "ct-*" } | ForEach-Object {
    docker volume rm $_ -f
    Write-Host "   Silindi: $_" -ForegroundColor Gray
}

# 3. Yeniden başlat
Write-Host "3. Servisler başlatılıyor..." -ForegroundColor Cyan
docker-compose --env-file ../.env up -d

# 4. Sağlık kontrolü
Write-Host "4. Servisler kontrol ediliyor (15 saniye bekleniyor)..." -ForegroundColor Cyan
Start-Sleep -Seconds 15
docker-compose --env-file ../.env ps

Write-Host ""
Write-Host "=== Hazır! ===" -ForegroundColor Green
Write-Host "PostgreSQL Main : localhost:$env:DB_PORT (varsayılan 5432)" -ForegroundColor White
Write-Host "PostgreSQL Audit: localhost:$env:AUDIT_DB_PORT (varsayılan 5433)" -ForegroundColor White
Write-Host "Redis           : localhost:$env:REDIS_PORT (varsayılan 6379)" -ForegroundColor White
Write-Host "pgAdmin         : http://localhost:$env:PGADMIN_PORT (varsayılan 5050)" -ForegroundColor White
Write-Host "Seq             : http://localhost:$env:SEQ_PORT (varsayılan 5341)" -ForegroundColor White
