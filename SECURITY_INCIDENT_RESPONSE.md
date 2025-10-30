# 🚨 SECURITY INCIDENT - IMMEDIATE ACTION REQUIRED

**Date:** 2025-10-30
**Severity:** CRITICAL
**Status:** IN PROGRESS

## Incident Summary

Sensitive credentials and API keys were accidentally committed and pushed to the public GitHub repository in commit `3779a049`.

**File:** `infrastructure/app-settings.sh`
**Exposed in:** https://github.com/ransierJ/WAiSA/blob/main/infrastructure/app-settings.sh

## 🔴 Exposed Credentials (Line Numbers)

### Line 5: SQL Server Password
```
Password=ilBSXDyoB6lcK340Aa1!
```
- **Service:** Azure SQL Database
- **Server:** waisa-poc-sql-hv2lph4y32udy.database.windows.net
- **Database:** WAiSADB
- **User:** waisaadmin
- **Action Required:** ROTATE IMMEDIATELY

### Line 22: Google API Key
```
GoogleSearch__ApiKey=AIzaSyAXVhm2EpVesSA_DGzPx2dlUrisFNxy790
```
- **Service:** Google Custom Search API
- **Action Required:** REVOKE AND ROTATE IMMEDIATELY

### Additional Keys Retrieved by Script
The script also retrieves and configures (but doesn't hardcode):
- Azure Cosmos DB connection strings
- Azure OpenAI API keys
- Azure Search admin keys
- Application Insights instrumentation keys

**Note:** While these aren't hardcoded, anyone with the resource names can potentially access them if not properly secured with RBAC.

## ⚡ IMMEDIATE ACTIONS REQUIRED (Priority Order)

### 1. Revoke Google API Key (HIGHEST PRIORITY)
```bash
# Go to Google Cloud Console
# https://console.cloud.google.com/apis/credentials

# Find key: AIzaSyAXVhm2EpVesSA_DGzPx2dlUrisFNxy790
# Click "Delete" or "Regenerate"

# Monitor usage for unauthorized access:
# https://console.cloud.google.com/apis/dashboard
```

### 2. Rotate SQL Server Password
```bash
# Change SQL Server admin password
az sql server update \
  --resource-group waisa-poc-rg \
  --name waisa-poc-sql-hv2lph4y32udy \
  --admin-password "NEW_SECURE_PASSWORD_HERE"

# Update Key Vault with new password
az keyvault secret set \
  --vault-name kvhv2lph4y32udy \
  --name "sql-admin-password" \
  --value "NEW_SECURE_PASSWORD_HERE"

# Update App Service configuration
az webapp config connection-string set \
  --resource-group waisa-poc-rg \
  --name waisa-poc-api-hv2lph4y32udy \
  --connection-string-type SQLAzure \
  --settings "SqlDatabase=Server=tcp:waisa-poc-sql-hv2lph4y32udy.database.windows.net,1433;Initial Catalog=WAiSADB;User ID=waisaadmin;Password=NEW_SECURE_PASSWORD_HERE"
```

### 3. Rotate Azure OpenAI Keys
```bash
# Regenerate Azure OpenAI key
az cognitiveservices account keys regenerate \
  --name waisa-poc-openai-hv2lph4y32udy \
  --resource-group waisa-poc-rg \
  --key-name key1

# Get new key
NEW_OPENAI_KEY=$(az cognitiveservices account keys list \
  --name waisa-poc-openai-hv2lph4y32udy \
  --resource-group waisa-poc-rg \
  --query "key1" -o tsv)

# Update Key Vault
az keyvault secret set \
  --vault-name kvhv2lph4y32udy \
  --name "openai-api-key" \
  --value "$NEW_OPENAI_KEY"

# Update App Service
az webapp config appsettings set \
  --resource-group waisa-poc-rg \
  --name waisa-poc-api-hv2lph4y32udy \
  --settings "AzureOpenAI__ApiKey=$NEW_OPENAI_KEY"
```

### 4. Rotate Azure Search Keys
```bash
# Regenerate Azure Search admin key
az search admin-key renew \
  --service-name waisa-poc-search-hv2lph4y32udy \
  --resource-group waisa-poc-rg \
  --key-kind primary

# Get new key
NEW_SEARCH_KEY=$(az search admin-key show \
  --service-name waisa-poc-search-hv2lph4y32udy \
  --resource-group waisa-poc-rg \
  --query "primaryKey" -o tsv)

# Update Key Vault
az keyvault secret set \
  --vault-name kvhv2lph4y32udy \
  --name "search-admin-key" \
  --value "$NEW_SEARCH_KEY"

# Update App Service
az webapp config appsettings set \
  --resource-group waisa-poc-rg \
  --name waisa-poc-api-hv2lph4y32udy \
  --settings "AzureSearch__AdminKey=$NEW_SEARCH_KEY"
```

### 5. Rotate Cosmos DB Keys
```bash
# Regenerate Cosmos DB primary key
az cosmosdb keys regenerate \
  --name waisa-poc-cosmos-hv2lph4y32udy \
  --resource-group waisa-poc-rg \
  --key-kind primary

# Get new connection string
NEW_COSMOS_CONN=$(az cosmosdb keys list \
  --name waisa-poc-cosmos-hv2lph4y32udy \
  --resource-group waisa-poc-rg \
  --type connection-strings \
  --query "connectionStrings[0].connectionString" -o tsv)

# Update Key Vault
az keyvault secret set \
  --vault-name kvhv2lph4y32udy \
  --name "cosmos-connection-string" \
  --value "$NEW_COSMOS_CONN"

# Update App Service
az webapp config appsettings set \
  --resource-group waisa-poc-rg \
  --name waisa-poc-api-hv2lph4y32udy \
  --settings "ConnectionStrings__CosmosDb=$NEW_COSMOS_CONN"
```

### 6. Remove File from Git History

The file is in commit history and must be completely removed:

```bash
# Install BFG Repo Cleaner (recommended method)
# Download from: https://rtyley.github.io/bfg-repo-cleaner/

# OR use git filter-repo (if installed)
pip install git-filter-repo

# Remove the file from history
git filter-repo --invert-paths --path infrastructure/app-settings.sh

# Force push (WARNING: This rewrites history)
git push origin main --force
```

**Alternative using git filter-branch:**
```bash
git filter-branch --force --index-filter \
  "git rm --cached --ignore-unmatch infrastructure/app-settings.sh" \
  --prune-empty --tag-name-filter cat -- --all

# Force push
git push origin main --force --all
```

### 7. Verify Cleanup
```bash
# Check that file is gone from history
git log --all --full-history -- infrastructure/app-settings.sh

# Should return empty
```

## 📊 Audit and Monitoring

### Check for Unauthorized Access

#### SQL Database
```bash
# Check SQL Server audit logs
az sql server audit-policy show \
  --resource-group waisa-poc-rg \
  --server waisa-poc-sql-hv2lph4y32udy

# Check recent logins (if auditing enabled)
# Review Application Insights for unusual database activity
```

#### Google API
```bash
# Check Google Cloud Console for:
# 1. API usage spikes
# 2. Requests from unexpected IPs
# 3. Quota consumption anomalies
```

#### Azure Resources
```bash
# Check Activity Log for unusual operations
az monitor activity-log list \
  --resource-group waisa-poc-rg \
  --start-time 2025-10-30T00:00:00Z \
  --query "[?contains(operationName.value, 'Microsoft.DocumentDB') || \
            contains(operationName.value, 'Microsoft.CognitiveServices')]" \
  --output table
```

## 🔒 Prevention Measures

### 1. Update .gitignore (DONE)
- Added `infrastructure/app-settings.sh`
- Added `infrastructure/*.sh`

### 2. Enable Pre-commit Hooks
Create `.git/hooks/pre-commit`:
```bash
#!/bin/bash
# Check for secrets before commit

if git diff --cached --name-only | grep -q "infrastructure.*\.sh"; then
    echo "ERROR: Attempting to commit infrastructure/*.sh file"
    echo "These files may contain secrets. Please review."
    exit 1
fi

# Check for common secret patterns
if git diff --cached | grep -iE "(password|api.?key|secret|token).*=.*['\"][^'\"]{8,}"; then
    echo "WARNING: Possible secret detected in commit"
    echo "Please review your changes carefully"
    exit 1
fi
```

### 3. Use GitHub Secret Scanning
- Already enabled (that's how we got the alert)
- GitHub will continue monitoring

### 4. Implement Proper Secret Management

**Going forward, NEVER hardcode secrets. Instead:**

```bash
# Store in Azure Key Vault
az keyvault secret set \
  --vault-name kvhv2lph4y32udy \
  --name "google-api-key" \
  --value "NEW_KEY_HERE"

# Reference in App Service
az webapp config appsettings set \
  --resource-group waisa-poc-rg \
  --name waisa-poc-api-hv2lph4y32udy \
  --settings "GoogleSearch__ApiKey=@Microsoft.KeyVault(SecretUri=https://kvhv2lph4y32udy.vault.azure.net/secrets/google-api-key/)"
```

## 📝 Incident Timeline

| Time | Action | Status |
|------|--------|--------|
| 2025-10-30 15:20 | Initial commit with secrets | ❌ EXPOSED |
| 2025-10-30 15:21 | Pushed to GitHub | ❌ PUBLIC |
| 2025-10-30 15:25 | GitHub secret scanning alert | ⚠️ DETECTED |
| 2025-10-30 15:26 | File removed from staging | 🔄 IN PROGRESS |
| TBD | Keys rotated | ⏳ PENDING |
| TBD | History cleaned | ⏳ PENDING |
| TBD | Force push completed | ⏳ PENDING |

## ✅ Verification Checklist

After completing all actions:

- [ ] Google API key revoked and new key created
- [ ] SQL Server password rotated
- [ ] Azure OpenAI keys rotated
- [ ] Azure Search keys rotated
- [ ] Cosmos DB keys rotated
- [ ] All services still functioning with new keys
- [ ] File removed from Git history
- [ ] Force push completed
- [ ] GitHub no longer shows the secret
- [ ] Audit logs reviewed for unauthorized access
- [ ] Pre-commit hooks installed
- [ ] Team notified of incident
- [ ] Incident report filed

## 📞 Contacts

- **Security Team:** [Contact Info]
- **Azure Support:** https://portal.azure.com/#blade/Microsoft_Azure_Support/HelpAndSupportBlade
- **Google Cloud Support:** https://cloud.google.com/support

## 🎓 Lessons Learned

1. **Never commit scripts with hardcoded credentials**
2. **Always use Key Vault for secrets**
3. **Use Key Vault references in App Service**
4. **Enable pre-commit hooks for secret detection**
5. **Review files before committing**
6. **Use .gitignore for sensitive file patterns**

## 📚 References

- [GitHub Secret Scanning](https://docs.github.com/en/code-security/secret-scanning)
- [Azure Key Vault Best Practices](https://learn.microsoft.com/en-us/azure/key-vault/general/best-practices)
- [BFG Repo Cleaner](https://rtyley.github.io/bfg-repo-cleaner/)
- [Removing Sensitive Data from Git](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/removing-sensitive-data-from-a-repository)

---

**Status:** ACTIVE INCIDENT - Immediate action required
**Next Review:** After all credentials rotated and verified
