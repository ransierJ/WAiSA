# Azure App Service Deployment Troubleshooting Guide

## Issue: Application Deployment Failures and 500 Errors

**Date Encountered:** October 27, 2025
**Severity:** High - Application completely unavailable

---

## Symptoms

- HTTP 500 errors on API endpoints (e.g., `/api/agents`)
- Health endpoint returns "Unhealthy"
- Application appears to be running but unresponsive
- Deployment commands succeed but application doesn't work
- Extremely long startup times (17+ minutes)
- Application logs stop updating

---

## Root Cause

**Azure App Service deployment state corruption** caused by:
- Failed or interrupted deployments leaving partial artifacts
- Cached files from previous deployments
- App Service not properly clearing old deployment files
- Conflicting deployment processes

This is NOT a code issue - the same code works after proper redeployment.

---

## Resolution Steps

### 1. Clean Local Build Artifacts

```bash
cd /path/to/WAiSA.API
rm -rf bin obj publish deploy.zip
```

### 2. Clean Rebuild

```bash
dotnet publish -c Release -o publish
```

Verify build succeeds with no errors (warnings are acceptable).

### 3. Create Deployment Package

```bash
cd publish
zip -r ../deploy.zip .
cd ..
```

### 4. Stop the Application

```bash
az webapp stop --name <app-name> --resource-group <resource-group>
```

Wait 10-15 seconds for graceful shutdown.

### 5. Deploy with Config-Zip Method

```bash
az webapp deployment source config-zip \
  --resource-group <resource-group> \
  --name <app-name> \
  --src ./deploy.zip
```

This method handles file cleanup better than standard `az webapp deploy`.

### 6. Start the Application (if needed)

```bash
az webapp start --name <app-name> --resource-group <resource-group>
```

### 7. Wait for Startup

Allow 2-5 minutes for normal startup, up to 20 minutes if recovering from corrupted state.

### 8. Verify Deployment

```bash
# Test health endpoint
curl https://<app-name>.azurewebsites.net/health

# Test API endpoints
curl https://<app-name>.azurewebsites.net/api/agents
```

---

## Prevention Tips

1. **Always use `--clean` flag** when deploying:
   ```bash
   az webapp deploy --clean true ...
   ```

2. **Stop app before major deployments** to prevent file locking

3. **Monitor deployment logs** for warnings about file conflicts

4. **Keep deployment packages clean** - always rebuild from scratch for production

5. **Use deployment slots** for zero-downtime deployments (when available)

6. **Verify health endpoint** immediately after deployment

---

## Alternative: Use Deployment Slots (Recommended for Production)

For production environments, use deployment slots to avoid downtime:

```bash
# Deploy to staging slot
az webapp deployment source config-zip \
  --resource-group <resource-group> \
  --name <app-name> \
  --slot staging \
  --src ./deploy.zip

# Test staging
curl https://<app-name>-staging.azurewebsites.net/health

# Swap to production
az webapp deployment slot swap \
  --resource-group <resource-group> \
  --name <app-name> \
  --slot staging \
  --target-slot production
```

---

## Quick Reference Commands

### Full Clean Deployment Script

```bash
#!/bin/bash
set -e

APP_NAME="waisa-poc-api-hv2lph4y32udy"
RESOURCE_GROUP="waisa-poc-rg"
API_PATH="/home/sysadmin/sysadmin_in_a_box/backend/WAiSA.API"

echo "Starting clean deployment..."

# Clean and build
cd "$API_PATH"
rm -rf bin obj publish deploy.zip
dotnet publish -c Release -o publish

# Create package
cd publish
zip -r ../deploy.zip .
cd ..

# Stop app
echo "Stopping application..."
az webapp stop --name "$APP_NAME" --resource-group "$RESOURCE_GROUP"
sleep 10

# Deploy
echo "Deploying application..."
az webapp deployment source config-zip \
  --resource-group "$RESOURCE_GROUP" \
  --name "$APP_NAME" \
  --src ./deploy.zip

# Start app (usually not needed as config-zip auto-starts)
echo "Starting application..."
az webapp start --name "$APP_NAME" --resource-group "$RESOURCE_GROUP"

# Wait and verify
echo "Waiting for startup..."
sleep 30

echo "Verifying deployment..."
curl -f "https://${APP_NAME}.azurewebsites.net/health" || echo "Health check failed!"

echo "Deployment complete!"
```

---

## Related Issues

- Azure App Service file locking on Windows
- Kudu deployment cache issues
- .NET assembly loading conflicts

---

## Additional Resources

- [Azure App Service Deployment Best Practices](https://learn.microsoft.com/en-us/azure/app-service/deploy-best-practices)
- [Troubleshoot Kudu Deployments](https://github.com/projectkudu/kudu/wiki)
- [App Service Diagnostics](https://learn.microsoft.com/en-us/azure/app-service/overview-diagnostics)

---

## Contact

If this issue persists after following all steps, check:
1. Azure App Service logs (`az webapp log download`)
2. Application Insights for startup errors
3. Cosmos DB connectivity
4. Environment variables configuration

**Last Updated:** October 27, 2025
