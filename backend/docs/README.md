# WAiSA Backend Documentation

This directory contains documentation for the WAiSA backend system.

## Contents

- **DEPLOYMENT_TROUBLESHOOTING.md** - Guide for resolving Azure App Service deployment issues
- **../scripts/clean-deploy.sh** - Automated clean deployment script

## Quick Start

### Deploying to Azure

For reliable deployments that avoid state corruption issues, use the clean deployment script:

```bash
cd /home/sysadmin/sysadmin_in_a_box/backend/scripts
./clean-deploy.sh
```

This script will:
1. Clean all build artifacts
2. Rebuild the application
3. Stop the Azure app
4. Deploy using config-zip method
5. Verify the deployment

### Troubleshooting Deployments

If you encounter issues like:
- 500 errors after deployment
- "Unhealthy" health status
- Application not responding
- Extremely long startup times

See **DEPLOYMENT_TROUBLESHOOTING.md** for detailed resolution steps.

## Key Learnings

### October 2025 Deployment Issue

We encountered a significant deployment issue where the application would return 500 errors despite successful deployment. The root cause was Azure App Service deployment state corruption, not code issues.

**Resolution:** Clean rebuild + config-zip deployment method

**Prevention:** Always use the clean-deploy.sh script for production deployments.

## Best Practices

1. **Always use clean deployments** for production
2. **Stop the app before major deployments** to prevent file locking
3. **Monitor the health endpoint** immediately after deployment
4. **Keep the deployment script updated** as infrastructure changes
5. **Use deployment slots** for production when possible

## Related Documentation

- Azure App Service: https://learn.microsoft.com/en-us/azure/app-service/
- Kudu Deployment: https://github.com/projectkudu/kudu/wiki

---

Last Updated: October 27, 2025
