# Google Custom Search API Setup Guide

## Overview

The WAiSA backend uses Google Custom Search API for web search functionality. This guide walks through obtaining the required credentials and configuring the Azure App Service.

## Why Google Custom Search?

- **Bing Search APIs retired**: Microsoft is retiring Bing Search APIs on August 11, 2025
- **Reliable alternative**: Google Custom Search provides reliable, programmable web search
- **Two-tier search**: Enables prioritized search (Microsoft Learn first, then web)

## Prerequisites

- Google account
- Google Cloud Platform project
- Azure CLI installed and authenticated

## Step 1: Create Google Cloud Project

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Click **Select a project** → **New Project**
3. Enter project name: `waisa-search`
4. Click **Create**

## Step 2: Enable Custom Search API

1. In Google Cloud Console, navigate to **APIs & Services** → **Library**
2. Search for "Custom Search API"
3. Click on **Custom Search API**
4. Click **Enable**

## Step 3: Create API Credentials

1. Navigate to **APIs & Services** → **Credentials**
2. Click **Create Credentials** → **API Key**
3. Copy the API key (you'll need this later)
4. **Recommended**: Click **Restrict Key** to limit usage:
   - Under "API restrictions", select "Restrict key"
   - Select "Custom Search API" from the dropdown
   - Click **Save**

## Step 4: Create Custom Search Engine

1. Go to [Programmable Search Engine](https://programmablesearchengine.google.com/)
2. Click **Get started** or **Add**
3. Configure your search engine:
   - **Name**: `WAiSA Web Search`
   - **What to search**: Select "Search the entire web"
   - **Search settings**: Leave defaults (safe search on, image search off)
4. Click **Create**
5. On the next screen, copy the **Search engine ID** (looks like: `a1b2c3d4e5f6g7h8i`)

## Step 5: Configure Azure App Service

### Using Azure Portal

1. Go to [Azure Portal](https://portal.azure.com)
2. Navigate to your App Service: `waisa-poc-api-hv2lph4y32udy`
3. Go to **Configuration** → **Application settings**
4. Click **New application setting** and add:
   - **Name**: `GoogleSearch__ApiKey`
   - **Value**: `<YOUR_GOOGLE_API_KEY>`
   - Click **OK**
5. Click **New application setting** again:
   - **Name**: `GoogleSearch__SearchEngineId`
   - **Value**: `<YOUR_SEARCH_ENGINE_ID>`
   - Click **OK**
6. Click **Save** at the top
7. Click **Continue** to confirm restart

### Using Azure CLI

Run these commands (replace `<YOUR_GOOGLE_API_KEY>` and `<YOUR_SEARCH_ENGINE_ID>`):

```bash
az webapp config appsettings set \
  --resource-group waisa-poc-rg \
  --name waisa-poc-api-hv2lph4y32udy \
  --settings \
    GoogleSearch__ApiKey="<YOUR_GOOGLE_API_KEY>" \
    GoogleSearch__SearchEngineId="<YOUR_SEARCH_ENGINE_ID>"
```

## Step 6: Verify Configuration

### Check Application Logs

```bash
az webapp log tail --resource-group waisa-poc-rg --name waisa-poc-api-hv2lph4y32udy
```

Look for this success message:
```
Google Custom Search client initialized
```

If you see this warning instead:
```
Google Custom Search API key or Search Engine ID not configured
```

Then the configuration settings are not yet applied or incorrect.

### Test Search Functionality

Send a test message through the web UI:
1. Open the web application
2. Send a message like "help troubleshoot Windows errors"
3. Watch the activity panel for:
   - 🔍 "Searching Microsoft Learn documentation..."
   - 🌐 "Searching the web for additional solutions..."

## Configuration Format

The backend expects these settings in .NET configuration format:

```json
{
  "GoogleSearch": {
    "ApiKey": "YOUR_GOOGLE_API_KEY",
    "SearchEngineId": "YOUR_SEARCH_ENGINE_ID"
  }
}
```

In Azure App Service, double underscores (`__`) represent nested configuration:
- `GoogleSearch__ApiKey` → `GoogleSearch:ApiKey`
- `GoogleSearch__SearchEngineId` → `GoogleSearch:SearchEngineId`

## Pricing Information

### Google Custom Search API Pricing

- **Free tier**: 100 queries per day
- **Paid tier**: $5 per 1,000 queries (after free tier)
- **Billing**: Charged to your Google Cloud project

**Estimate for WAiSA**:
- Average 2 searches per user message (Microsoft Learn + web)
- 50 user messages/day = 100 searches/day = **FREE**
- 500 user messages/day = 1,000 searches/day = **$5/day** (~$150/month)

### Cost Management

To stay within free tier:
1. Set quota limits in Google Cloud Console
2. Monitor usage in Google Cloud Console → APIs & Services → Dashboard
3. Consider caching search results for common queries

## Troubleshooting

### Error: "Google Custom Search not configured"

**Cause**: Missing or incorrect API credentials

**Solution**: Verify Azure App Service configuration settings exist and are spelled correctly

### Error: "API key not valid"

**Cause**: Invalid API key or API not enabled

**Solution**:
1. Verify Custom Search API is enabled in Google Cloud Console
2. Check API key is copied correctly (no extra spaces)
3. Ensure API key restrictions don't block Custom Search API

### Error: "Invalid search engine ID"

**Cause**: Search engine ID is incorrect or search engine was deleted

**Solution**:
1. Go to https://programmablesearchengine.google.com/
2. Verify your search engine still exists
3. Copy the correct search engine ID

### No Search Results

**Cause**: Search engine might be configured to search only specific sites

**Solution**:
1. Go to https://programmablesearchengine.google.com/
2. Edit your search engine
3. Ensure "Search the entire web" is enabled
4. Remove any site restrictions unless intentional

## Search Behavior

### Microsoft Learn Search

When searching Microsoft documentation:
- Query is prefixed with `site:learn.microsoft.com`
- Only returns results from Microsoft's official documentation
- Example: `site:learn.microsoft.com PowerShell Get-EventLog`

### Web Search

When searching the broader web:
- Uses full web index
- Returns community forums, blogs, Stack Overflow, etc.
- Provides additional context beyond official docs

### Search Priority

The AI follows this search workflow:
1. **First**: Search Microsoft Learn (official, trusted sources)
2. **Second**: Search web (community solutions, additional context)
3. **Combined**: Uses both sources for comprehensive answers

This ensures the AI prioritizes official Microsoft documentation while still accessing community knowledge when needed.

## Security Best Practices

1. **Restrict API key**: Limit to Custom Search API only
2. **Monitor usage**: Set up billing alerts in Google Cloud
3. **Rotate keys**: Periodically generate new API keys
4. **Environment variables**: Never commit API keys to source control
5. **Azure Key Vault**: For production, store API keys in Azure Key Vault

## Support

- **Google Custom Search API Docs**: https://developers.google.com/custom-search/v1/overview
- **Google Cloud Console**: https://console.cloud.google.com/
- **Programmable Search Engine**: https://programmablesearchengine.google.com/

## Implementation Details

### Code Location

- **SearchService.cs**: `WAiSA.Infrastructure/Services/SearchService.cs`
- **Configuration**: Lines 33-48
- **Web Search**: Lines 95-154
- **Microsoft Learn Search**: Lines 51-93

### Dependencies

- **Package**: `Google.Apis.CustomSearchAPI.v1` v1.68.0.3520
- **Required**: `Google.Apis.Services`, `Google.Apis.Core`
- **Project**: `WAiSA.Infrastructure.csproj`

### Service Registration

In `Program.cs`:
```csharp
builder.Services.AddHttpClient<ISearchService, SearchService>();
```

The HttpClient factory provides automatic connection pooling and management.
