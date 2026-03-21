# 1. Docker volume sýfýrla
cd D:\Projects\CleanTenant\docker
docker-compose --env-file ../.env down -v
docker-compose --env-file ../.env up -d
Start-Sleep -Seconds 15
cd ..

# 2. Eski migration'larý sil ve yeniden oluþtur
dotnet ef migrations remove --project src/CleanTenant.Infrastructure --startup-project src/CleanTenant.API --context ApplicationDbContext --force
dotnet ef migrations remove --project src/CleanTenant.Infrastructure --startup-project src/CleanTenant.API --context AuditDbContext --force

dotnet ef migrations add InitialCreate --project src/CleanTenant.Infrastructure --startup-project src/CleanTenant.API --context ApplicationDbContext --output-dir Persistence/Migrations/Main
dotnet ef migrations add InitialCreate --project src/CleanTenant.Infrastructure --startup-project src/CleanTenant.API --context AuditDbContext --output-dir Persistence/Migrations/Audit

dotnet ef database update --project src/CleanTenant.Infrastructure --startup-project src/CleanTenant.API --context ApplicationDbContext
dotnet ef database update --project src/CleanTenant.Infrastructure --startup-project src/CleanTenant.API --context AuditDbContext

# 3. API'yi baþlat
dotnet run --project src/CleanTenant.API