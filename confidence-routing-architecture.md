# Confidence-Based Information Source Routing Architecture

## Executive Summary

This document outlines a hybrid orchestrator-pipeline architecture for intelligent information source routing based on confidence scoring. The system optimizes for accuracy, latency, and cost by dynamically selecting the best information source.

---

## 1. Architecture Pattern Selection

### Recommended: **Hybrid Orchestrator-Pipeline Pattern**

**Rationale:**
- **Orchestrator** handles decision logic, confidence aggregation, and source selection
- **Pipeline stages** execute source queries with early termination capability
- **Strategy pattern** allows pluggable confidence calculation and routing rules

### Alternative Patterns Considered:

1. **Pure Pipeline**: Too rigid, difficult to implement early termination
2. **Decision Tree**: Brittle, requires manual tuning for each query type
3. **Chain of Responsibility**: Limited observability, hard to implement parallel queries

---

## 2. High-Level System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     API Gateway / Router                     │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                  Query Orchestrator                          │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  - Query Classification                               │  │
│  │  - Routing Strategy Selection                         │  │
│  │  - Confidence Threshold Management                    │  │
│  │  - Result Aggregation                                 │  │
│  └──────────────────────────────────────────────────────┘  │
└───┬─────────────┬─────────────┬─────────────┬──────────────┘
    │             │             │             │
    ▼             ▼             ▼             ▼
┌─────────┐  ┌─────────┐  ┌──────────┐  ┌─────────┐
│   KB    │  │   LLM   │  │ MS Docs  │  │   Web   │
│ Source  │  │ Source  │  │  Source  │  │ Search  │
└─────────┘  └─────────┘  └──────────┘  └─────────┘
    │             │             │             │
    └─────────────┴─────────────┴─────────────┘
                         │
                         ▼
              ┌────────────────────┐
              │  Response Builder  │
              └────────────────────┘
                         │
                         ▼
              ┌────────────────────┐
              │   Cache Layer      │
              │   (Redis/Memory)   │
              └────────────────────┘
```

---

## 3. Core Components

### 3.1 Query Orchestrator

The brain of the system that manages source selection and confidence evaluation.

**Responsibilities:**
- Query classification and routing strategy selection
- Source execution coordination (sequential/parallel)
- Confidence score aggregation
- Early termination logic
- Result combination and ranking

**Key Interfaces:**
```typescript
interface QueryOrchestrator {
  route(query: Query): Promise<RoutedResponse>;
  selectStrategy(query: Query): RoutingStrategy;
  aggregateResults(results: SourceResult[]): AggregatedResult;
}
```

### 3.2 Information Sources

Each source implements a common interface with confidence scoring:

```typescript
interface InformationSource {
  name: string;
  priority: number;
  averageLatency: number;
  costPerQuery: number;

  query(request: QueryRequest): Promise<SourceResult>;
  calculateConfidence(result: any, context: QueryContext): number;
  canHandle(query: Query): boolean;
}

interface SourceResult {
  source: string;
  confidence: number;  // 0-100
  answer: string;
  metadata: {
    latency: number;
    tokensUsed?: number;
    documentsSearched?: number;
  };
  reasoning: string;  // Why this confidence score?
}
```

### 3.3 Routing Strategies

Different strategies for different query types:

```typescript
enum RoutingStrategy {
  SEQUENTIAL_SHORT_CIRCUIT,  // Stop at first high-confidence
  SEQUENTIAL_EXHAUSTIVE,     // Query all, pick best
  PARALLEL_RACE,             // Query all parallel, use first high-confidence
  PARALLEL_AGGREGATE,        // Query all parallel, combine results
  ADAPTIVE,                  // Learn from patterns
}
```

---

## 4. Routing Strategy Details

### Strategy 1: Sequential Short-Circuit (Default)

**Best for:** Most queries where cost/latency matter

```
Query KB → [confidence ≥ threshold?]
           ├─ Yes → Return result
           └─ No  → Query LLM → [confidence ≥ threshold?]
                                 ├─ Yes → Return result
                                 └─ No  → Query MS Docs → [confidence ≥ threshold?]
                                                           ├─ Yes → Return result
                                                           └─ No  → Query Web → Return best
```

**Thresholds:**
- KB: 85% (high confidence needed for cached/structured data)
- LLM: 75% (moderate confidence acceptable)
- MS Docs: 70% (documentation often authoritative)
- Web: Return best available (no threshold)

**Advantages:**
- Minimal latency for common queries
- Cost-effective (fewer API calls)
- Predictable performance

**Disadvantages:**
- May miss better answers from later sources
- Sequential latency accumulation

### Strategy 2: Parallel Aggregate

**Best for:** Critical queries where accuracy matters most

```
┌─ Query KB ────┐
├─ Query LLM ───┤
├─ Query MS Docs┤  → Wait for all → Aggregate by confidence → Return best/combined
└─ Query Web ───┘
```

**Aggregation Logic:**
```
1. Collect all results
2. Normalize confidence scores
3. If top confidence > 90%, return single answer
4. If multiple sources 70-90%, combine weighted by confidence
5. If all < 70%, return ensemble with warnings
```

**Advantages:**
- Best possible answer
- Can combine complementary information
- Fault tolerance (if one source fails)

**Disadvantages:**
- Higher latency (slowest source)
- Higher cost (all APIs called)
- Complex aggregation logic

### Strategy 3: Adaptive Routing

**Best for:** Production systems with learning capability

```
Query Classification → Historical Performance Lookup → Dynamic Strategy Selection
                                                              ↓
                                                    [Sequential | Parallel | Hybrid]
```

**Learning Features:**
- Track which sources answer query types best
- Learn optimal confidence thresholds per source
- Predict best strategy based on query patterns
- A/B test routing strategies

---

## 5. Confidence Score Calibration

### 5.1 Challenge: Different Sources, Different Scales

Each source has different confidence semantics:

| Source | Raw Confidence Basis | Typical Range |
|--------|---------------------|---------------|
| KB | Exact match + freshness | 60-100% |
| LLM | Model uncertainty + grounding | 40-95% |
| MS Docs | Relevance score + authority | 50-90% |
| Web | PageRank + snippet match | 30-80% |

### 5.2 Calibration Approach: Feature-Based Normalization

```typescript
interface ConfidenceCalculator {
  calculateConfidence(result: RawResult, features: ConfidenceFeatures): number;
}

interface ConfidenceFeatures {
  // Source-specific features
  exactMatch: boolean;
  partialMatch: boolean;
  freshness: number;  // 0-1, how recent is data
  authority: number;  // 0-1, source authority

  // Answer quality features
  answerLength: number;
  structureQuality: number;  // Well-formatted?
  citationCount: number;

  // Context features
  queryComplexity: number;
  domainMatch: boolean;
  historicalAccuracy: number;  // How often has this source been right?
}
```

### 5.3 Source-Specific Confidence Calculations

#### Knowledge Base Confidence
```typescript
function calculateKBConfidence(result: KBResult): number {
  let confidence = 0;

  // Exact match: high confidence
  if (result.exactMatch) {
    confidence = 90;
  } else if (result.partialMatch) {
    confidence = 70;
  } else {
    confidence = 50;
  }

  // Adjust for freshness (exponential decay)
  const daysSinceUpdate = result.daysSinceUpdate;
  const freshnessMultiplier = Math.exp(-daysSinceUpdate / 180); // Half-life 180 days
  confidence *= (0.7 + 0.3 * freshnessMultiplier);

  // Boost for multiple confirming documents
  if (result.confirmingDocs > 1) {
    confidence = Math.min(100, confidence * 1.1);
  }

  return Math.round(confidence);
}
```

#### LLM Confidence
```typescript
function calculateLLMConfidence(result: LLMResult): number {
  let confidence = result.modelConfidence || 60;  // Base model uncertainty

  // Check for hedging language
  const hedgePhrases = ['might', 'possibly', 'unsure', 'probably'];
  const hasHedging = hedgePhrases.some(phrase =>
    result.answer.toLowerCase().includes(phrase)
  );
  if (hasHedging) {
    confidence *= 0.8;
  }

  // Check for grounding (citations)
  if (result.citations && result.citations.length > 0) {
    confidence = Math.min(100, confidence * 1.2);
  }

  // Penalize if answer is too short or too long
  const answerLength = result.answer.length;
  if (answerLength < 50) {
    confidence *= 0.9;
  } else if (answerLength > 2000) {
    confidence *= 0.95;  // May be rambling
  }

  return Math.round(confidence);
}
```

#### MS Docs Confidence
```typescript
function calculateMSDocsConfidence(result: DocResult): number {
  // Base on search relevance score
  let confidence = result.relevanceScore * 100;  // 0-1 to 0-100

  // Official Microsoft docs: high authority
  if (result.isOfficialDocs) {
    confidence = Math.min(100, confidence * 1.3);
  }

  // Check document freshness
  const monthsSinceUpdate = result.monthsSinceUpdate;
  if (monthsSinceUpdate < 6) {
    confidence = Math.min(100, confidence * 1.1);
  } else if (monthsSinceUpdate > 24) {
    confidence *= 0.85;  // May be outdated
  }

  // Check for version match
  if (result.versionMatches) {
    confidence = Math.min(100, confidence * 1.15);
  }

  return Math.round(confidence);
}
```

#### Web Search Confidence
```typescript
function calculateWebConfidence(result: WebResult): number {
  // Start with search ranking
  let confidence = 40 + (1 - result.position / 10) * 30;  // Position 1: 70%, Position 10: 40%

  // Domain authority
  const trustedDomains = ['stackoverflow.com', 'github.com', 'microsoft.com'];
  if (trustedDomains.some(domain => result.url.includes(domain))) {
    confidence = Math.min(100, confidence * 1.4);
  }

  // Snippet quality
  if (result.snippet && result.snippet.length > 100) {
    confidence *= 1.1;
  }

  // Recency (for time-sensitive queries)
  if (result.isRecent) {
    confidence = Math.min(100, confidence * 1.15);
  }

  return Math.round(confidence);
}
```

### 5.4 Cross-Source Normalization

Apply final normalization to ensure fair comparison:

```typescript
function normalizeConfidenceScores(results: SourceResult[]): SourceResult[] {
  // Calculate historical accuracy for each source
  const sourceAccuracy = getHistoricalAccuracy();  // From analytics

  return results.map(result => {
    const accuracy = sourceAccuracy[result.source] || 0.8;
    const calibratedConfidence = result.confidence * accuracy;

    return {
      ...result,
      confidence: Math.round(calibratedConfidence),
      originalConfidence: result.confidence
    };
  });
}
```

---

## 6. Edge Case Handling

### 6.1 All Sources Low Confidence

**Scenario:** All sources return < 70% confidence

**Strategy:**
```typescript
function handleLowConfidenceScenario(results: SourceResult[]): Response {
  // Sort by confidence
  const sorted = results.sort((a, b) => b.confidence - a.confidence);

  return {
    answer: sorted[0].answer,
    confidence: sorted[0].confidence,
    warning: "Low confidence - please verify this answer",
    alternatives: sorted.slice(1, 3),  // Show runner-ups
    suggestedAction: "ConsultHumanExpert"
  };
}
```

### 6.2 Confidence Ties

**Scenario:** Two sources return similar confidence (within 5%)

**Strategy:**
```typescript
function handleConfidenceTie(results: SourceResult[]): Response {
  const topResults = results.filter(r =>
    r.confidence >= results[0].confidence - 5
  );

  if (topResults.length === 1) {
    return createResponse(topResults[0]);
  }

  // Check if answers agree
  const answersAgree = checkAnswerSimilarity(
    topResults.map(r => r.answer)
  );

  if (answersAgree) {
    // Combine with weighted average
    return combineAnswers(topResults);
  } else {
    // Return all with disambiguation
    return {
      answer: null,
      multipleAnswers: topResults,
      message: "Multiple high-confidence answers found - please review",
      suggestedAction: "UserDisambiguation"
    };
  }
}
```

### 6.3 Conflicting Answers

**Scenario:** High-confidence sources give different answers

**Strategy:**
```typescript
function handleConflictingAnswers(results: SourceResult[]): Response {
  // Detect semantic conflict
  const conflict = detectSemanticConflict(results);

  if (!conflict) {
    return combineComplementaryAnswers(results);
  }

  // True conflict - use tie-breaker rules
  const tieBreakers = [
    { rule: 'PreferMoreRecentSource', weight: 0.3 },
    { rule: 'PreferHigherAuthority', weight: 0.4 },
    { rule: 'PreferMoreSpecific', weight: 0.3 }
  ];

  const winner = applyTieBreakers(results, tieBreakers);

  return {
    answer: winner.answer,
    confidence: winner.confidence - 10,  // Reduce due to conflict
    conflict: true,
    conflictingAnswers: results.filter(r => r !== winner),
    resolution: "Selected based on recency and authority"
  };
}
```

### 6.4 Source Failures

**Scenario:** One or more sources timeout or error

**Strategy:**
```typescript
async function queryWithFallback(
  source: InformationSource,
  query: Query,
  timeout: number
): Promise<SourceResult | null> {
  try {
    const result = await Promise.race([
      source.query(query),
      timeoutPromise(timeout)
    ]);
    return result;
  } catch (error) {
    logger.error(`Source ${source.name} failed`, error);
    metrics.increment(`source.${source.name}.failures`);

    // Continue with other sources
    return null;
  }
}

function aggregateWithFailures(results: (SourceResult | null)[]): Response {
  const validResults = results.filter(r => r !== null);

  if (validResults.length === 0) {
    return {
      error: "All sources failed",
      fallbackAnswer: "Please try again or contact support"
    };
  }

  // Continue with valid results
  return selectBestResult(validResults);
}
```

### 6.5 Ambiguous Queries

**Scenario:** Query could mean multiple things

**Strategy:**
```typescript
function handleAmbiguousQuery(query: Query): Response {
  // Detect ambiguity
  const disambiguations = detectAmbiguity(query);

  if (disambiguations.length > 1) {
    return {
      needsDisambiguation: true,
      options: disambiguations.map(d => ({
        interpretation: d.interpretation,
        confidence: d.confidence,
        preview: d.preview
      })),
      message: "Your query could mean multiple things. Please select:"
    };
  }

  // Proceed with routing
  return routeQuery(query);
}
```

---

## 7. Performance Optimization

### 7.1 Caching Strategy

**Multi-Layer Caching:**

```
L1: In-Memory Cache (Redis)
├─ Query Results (TTL: 1 hour)
├─ Confidence Scores (TTL: 24 hours)
└─ Source Availability (TTL: 5 minutes)

L2: Distributed Cache (Redis Cluster)
├─ Historical Analytics (TTL: 7 days)
└─ Popular Queries (TTL: 24 hours)

L3: Database Cache (PostgreSQL)
├─ Query Patterns
└─ Source Performance Metrics
```

**Cache Key Strategy:**
```typescript
function generateCacheKey(query: Query, sources: string[]): string {
  // Normalize query for better cache hits
  const normalized = normalizeQuery(query.text);
  const sourceHash = hashSources(sources);

  return `query:${hash(normalized)}:sources:${sourceHash}`;
}

function normalizeQuery(text: string): string {
  return text
    .toLowerCase()
    .trim()
    .replace(/\s+/g, ' ')
    .replace(/[^\w\s]/g, '');  // Remove punctuation
}
```

**Cache Warming:**
```typescript
async function warmCache() {
  // Pre-populate cache with common queries
  const commonQueries = await getCommonQueries(limit=1000);

  for (const query of commonQueries) {
    await routeQuery(query, { background: true });
  }
}
```

### 7.2 Parallel Query Optimization

**Concurrent Execution with Timeouts:**

```typescript
async function querySourcesParallel(
  sources: InformationSource[],
  query: Query,
  options: ParallelOptions
): Promise<SourceResult[]> {
  const promises = sources.map(source =>
    queryWithTimeout(source, query, options.timeout)
  );

  if (options.earlyReturn) {
    // Return as soon as we get high confidence
    return raceWithConfidenceThreshold(promises, options.threshold);
  } else {
    // Wait for all (or timeout)
    return Promise.allSettled(promises).then(extractResults);
  }
}

async function raceWithConfidenceThreshold(
  promises: Promise<SourceResult>[],
  threshold: number
): Promise<SourceResult[]> {
  const results: SourceResult[] = [];

  for await (const result of iterateAsCompleted(promises)) {
    results.push(result);

    if (result.confidence >= threshold) {
      // Found high-confidence answer, return immediately
      return results;
    }
  }

  return results;  // All completed, none exceeded threshold
}
```

### 7.3 Request Batching

**Batch Similar Queries:**

```typescript
class QueryBatcher {
  private batch: Query[] = [];
  private timer: NodeJS.Timeout | null = null;

  async addQuery(query: Query): Promise<Response> {
    this.batch.push(query);

    if (this.batch.length >= 10) {
      return this.flush();
    }

    if (!this.timer) {
      this.timer = setTimeout(() => this.flush(), 100);  // 100ms window
    }

    return this.waitForResult(query);
  }

  private async flush() {
    const queries = this.batch;
    this.batch = [];
    this.timer = null;

    // Group similar queries
    const groups = groupSimilarQueries(queries);

    // Execute each group in parallel
    await Promise.all(
      groups.map(group => this.executeGroup(group))
    );
  }
}
```

### 7.4 Connection Pooling

**Reuse Connections:**

```typescript
class SourceConnectionPool {
  private pools: Map<string, ConnectionPool> = new Map();

  getConnection(source: string): Connection {
    if (!this.pools.has(source)) {
      this.pools.set(source, new ConnectionPool({
        min: 2,
        max: 10,
        acquireTimeout: 5000,
        idleTimeout: 30000
      }));
    }

    return this.pools.get(source)!.acquire();
  }
}
```

### 7.5 Adaptive Timeouts

**Learn Optimal Timeouts:**

```typescript
class AdaptiveTimeoutManager {
  private percentiles: Map<string, number[]> = new Map();

  getTimeout(source: string, percentile: number = 95): number {
    const latencies = this.percentiles.get(source) || [1000];
    return calculatePercentile(latencies, percentile) * 1.2;  // 20% buffer
  }

  recordLatency(source: string, latency: number) {
    if (!this.percentiles.has(source)) {
      this.percentiles.set(source, []);
    }

    const latencies = this.percentiles.get(source)!;
    latencies.push(latency);

    // Keep only last 1000 samples
    if (latencies.length > 1000) {
      latencies.shift();
    }
  }
}
```

---

## 8. Extensibility Design

### 8.1 Plugin Architecture

**Easy Addition of New Sources:**

```typescript
// Base interface
abstract class InformationSourcePlugin {
  abstract name: string;
  abstract priority: number;

  abstract initialize(): Promise<void>;
  abstract query(request: QueryRequest): Promise<SourceResult>;
  abstract calculateConfidence(result: any): number;
  abstract healthCheck(): Promise<boolean>;

  // Optional overrides
  canHandle(query: Query): boolean { return true; }
  getCost(): number { return 0; }
  getAverageLatency(): number { return 1000; }
}

// Example: New Slack Search Source
class SlackSearchSource extends InformationSourcePlugin {
  name = 'slack';
  priority = 2;  // Between KB and LLM

  async initialize() {
    this.client = new SlackClient(process.env.SLACK_TOKEN);
  }

  async query(request: QueryRequest): Promise<SourceResult> {
    const results = await this.client.search(request.query);
    const bestMatch = results[0];

    return {
      source: this.name,
      confidence: this.calculateConfidence(bestMatch),
      answer: bestMatch.text,
      metadata: {
        channel: bestMatch.channel,
        timestamp: bestMatch.timestamp
      }
    };
  }

  calculateConfidence(result: any): number {
    // Custom confidence logic for Slack
    return result.score * 80;  // Slack scores are 0-1
  }
}

// Register plugin
SourceRegistry.register(new SlackSearchSource());
```

### 8.2 Configuration-Driven Routing

**Define Rules in Config:**

```yaml
# routing-config.yaml
routing_strategies:
  default:
    type: sequential_short_circuit
    sources:
      - name: kb
        threshold: 85
        timeout: 500ms
      - name: llm
        threshold: 75
        timeout: 3000ms
      - name: ms_docs
        threshold: 70
        timeout: 2000ms
      - name: web
        threshold: 0
        timeout: 5000ms

  critical:
    type: parallel_aggregate
    sources: [kb, llm, ms_docs, web]
    aggregation: weighted_ensemble
    min_confidence: 80

  fast:
    type: sequential_short_circuit
    sources: [kb, llm]
    max_latency: 2000ms

query_classification:
  rules:
    - pattern: "urgent|critical|important"
      strategy: critical
    - pattern: "quick question|briefly"
      strategy: fast
    - default: default
```

### 8.3 Strategy Factory

**Dynamic Strategy Selection:**

```typescript
class StrategyFactory {
  private strategies: Map<string, RoutingStrategy> = new Map();

  registerStrategy(name: string, strategy: RoutingStrategy) {
    this.strategies.set(name, strategy);
  }

  getStrategy(query: Query, config: Config): RoutingStrategy {
    // Check query for strategy hints
    for (const rule of config.query_classification.rules) {
      if (query.text.match(rule.pattern)) {
        return this.strategies.get(rule.strategy)!;
      }
    }

    // Default strategy
    return this.strategies.get(config.query_classification.default)!;
  }
}
```

### 8.4 Middleware Pipeline

**Extensible Processing:**

```typescript
interface Middleware {
  process(context: QueryContext, next: () => Promise<Response>): Promise<Response>;
}

class LoggingMiddleware implements Middleware {
  async process(context: QueryContext, next: () => Promise<Response>) {
    const start = Date.now();
    logger.info('Query started', { query: context.query });

    const response = await next();

    logger.info('Query completed', {
      query: context.query,
      duration: Date.now() - start,
      confidence: response.confidence
    });

    return response;
  }
}

class RateLimitMiddleware implements Middleware {
  async process(context: QueryContext, next: () => Promise<Response>) {
    const allowed = await this.checkRateLimit(context.userId);

    if (!allowed) {
      throw new RateLimitError();
    }

    return next();
  }
}

// Build pipeline
const pipeline = new MiddlewarePipeline()
  .use(new LoggingMiddleware())
  .use(new AuthenticationMiddleware())
  .use(new RateLimitMiddleware())
  .use(new CachingMiddleware())
  .use(new QueryRoutingMiddleware());
```

---

## 9. Complete Pseudocode Implementation

```typescript
// ============================================================================
// MAIN ORCHESTRATOR
// ============================================================================

class ConfidenceBasedOrchestrator {
  private sources: Map<string, InformationSource>;
  private cache: CacheManager;
  private metrics: MetricsCollector;
  private config: Config;

  async route(query: Query, options?: RoutingOptions): Promise<Response> {
    // 1. Check cache
    const cached = await this.cache.get(query);
    if (cached && !options?.bypassCache) {
      this.metrics.recordCacheHit();
      return cached;
    }

    // 2. Classify query and select strategy
    const classification = await this.classifyQuery(query);
    const strategy = this.selectStrategy(classification);

    // 3. Execute routing strategy
    const results = await strategy.execute(query, this.sources);

    // 4. Aggregate and select best result
    const response = await this.aggregateResults(results, strategy);

    // 5. Cache result
    await this.cache.set(query, response, this.getTTL(response.confidence));

    // 6. Record metrics
    this.metrics.recordQuery(query, response, results);

    return response;
  }

  private async classifyQuery(query: Query): Promise<QueryClassification> {
    return {
      complexity: estimateComplexity(query),
      domain: detectDomain(query),
      urgency: detectUrgency(query),
      type: detectQueryType(query)  // factual, procedural, diagnostic, etc.
    };
  }

  private selectStrategy(classification: QueryClassification): RoutingStrategy {
    if (classification.urgency === 'critical') {
      return new ParallelAggregateStrategy(this.config.strategies.critical);
    }

    if (classification.complexity === 'simple') {
      return new SequentialShortCircuitStrategy(this.config.strategies.fast);
    }

    return new SequentialShortCircuitStrategy(this.config.strategies.default);
  }

  private async aggregateResults(
    results: SourceResult[],
    strategy: RoutingStrategy
  ): Promise<Response> {
    // Filter out failed sources
    const valid = results.filter(r => r !== null && r.confidence > 0);

    if (valid.length === 0) {
      return this.handleNoResults();
    }

    // Normalize confidence scores
    const normalized = this.normalizeConfidence(valid);

    // Sort by confidence
    const sorted = normalized.sort((a, b) => b.confidence - a.confidence);

    // Check for edge cases
    if (sorted[0].confidence < 70) {
      return this.handleLowConfidence(sorted);
    }

    if (sorted.length > 1 && Math.abs(sorted[0].confidence - sorted[1].confidence) < 5) {
      return this.handleTie(sorted);
    }

    // Check for conflicts
    const conflict = this.detectConflict(sorted.slice(0, 3));
    if (conflict) {
      return this.handleConflict(sorted);
    }

    // Return best result
    return this.buildResponse(sorted[0], sorted.slice(1, 3));
  }

  private normalizeConfidence(results: SourceResult[]): SourceResult[] {
    // Get historical accuracy for each source
    const accuracy = this.metrics.getSourceAccuracy();

    return results.map(result => ({
      ...result,
      originalConfidence: result.confidence,
      confidence: Math.round(result.confidence * (accuracy[result.source] || 0.8))
    }));
  }

  private detectConflict(results: SourceResult[]): boolean {
    if (results.length < 2) return false;

    // Simple semantic similarity check
    for (let i = 0; i < results.length - 1; i++) {
      for (let j = i + 1; j < results.length; j++) {
        const similarity = calculateSimilarity(results[i].answer, results[j].answer);
        if (similarity < 0.5 && results[i].confidence > 70 && results[j].confidence > 70) {
          return true;  // High confidence but different answers
        }
      }
    }

    return false;
  }

  private buildResponse(primary: SourceResult, alternatives: SourceResult[]): Response {
    return {
      answer: primary.answer,
      confidence: primary.confidence,
      source: primary.source,
      metadata: primary.metadata,
      reasoning: primary.reasoning,
      alternatives: alternatives.map(a => ({
        answer: a.answer,
        source: a.source,
        confidence: a.confidence
      })),
      timestamp: new Date().toISOString()
    };
  }
}

// ============================================================================
// ROUTING STRATEGIES
// ============================================================================

class SequentialShortCircuitStrategy implements RoutingStrategy {
  constructor(private config: StrategyConfig) {}

  async execute(
    query: Query,
    sources: Map<string, InformationSource>
  ): Promise<SourceResult[]> {
    const results: SourceResult[] = [];

    for (const sourceConfig of this.config.sources) {
      const source = sources.get(sourceConfig.name);
      if (!source) continue;

      // Check if source can handle this query
      if (!source.canHandle(query)) {
        continue;
      }

      // Query source with timeout
      const result = await this.queryWithTimeout(
        source,
        query,
        sourceConfig.timeout
      );

      if (result) {
        results.push(result);

        // Early termination if confidence threshold met
        if (result.confidence >= sourceConfig.threshold) {
          logger.info(`Short-circuit: ${source.name} confidence ${result.confidence}%`);
          break;
        }
      }
    }

    return results;
  }

  private async queryWithTimeout(
    source: InformationSource,
    query: Query,
    timeout: number
  ): Promise<SourceResult | null> {
    try {
      const timeoutPromise = new Promise((_, reject) =>
        setTimeout(() => reject(new Error('Timeout')), timeout)
      );

      const result = await Promise.race([
        source.query({ query }),
        timeoutPromise
      ]) as SourceResult;

      return result;
    } catch (error) {
      logger.error(`Source ${source.name} failed`, error);
      return null;
    }
  }
}

class ParallelAggregateStrategy implements RoutingStrategy {
  constructor(private config: StrategyConfig) {}

  async execute(
    query: Query,
    sources: Map<string, InformationSource>
  ): Promise<SourceResult[]> {
    // Query all sources in parallel
    const promises = Array.from(sources.values())
      .filter(source => source.canHandle(query))
      .map(source => this.querySource(source, query));

    // Wait for all with individual timeouts
    const results = await Promise.allSettled(promises);

    // Extract successful results
    return results
      .filter(r => r.status === 'fulfilled')
      .map(r => (r as PromiseFulfilledResult<SourceResult>).value)
      .filter(r => r !== null);
  }

  private async querySource(
    source: InformationSource,
    query: Query
  ): Promise<SourceResult | null> {
    const timeout = this.config.timeout || 5000;

    try {
      return await Promise.race([
        source.query({ query }),
        this.timeoutPromise(timeout)
      ]);
    } catch (error) {
      logger.error(`Source ${source.name} failed`, error);
      return null;
    }
  }

  private timeoutPromise(ms: number): Promise<never> {
    return new Promise((_, reject) =>
      setTimeout(() => reject(new Error('Timeout')), ms)
    );
  }
}

class AdaptiveStrategy implements RoutingStrategy {
  constructor(
    private config: StrategyConfig,
    private analytics: AnalyticsService
  ) {}

  async execute(
    query: Query,
    sources: Map<string, InformationSource>
  ): Promise<SourceResult[]> {
    // Analyze historical performance for this query type
    const queryType = classifyQueryType(query);
    const performance = await this.analytics.getPerformance(queryType);

    // Rank sources by historical success
    const rankedSources = this.rankSources(sources, performance);

    // Decide: sequential or parallel?
    const shouldParallelize = this.shouldParallelize(performance, query);

    if (shouldParallelize) {
      return new ParallelAggregateStrategy(this.config).execute(query, sources);
    } else {
      // Dynamic thresholds based on learning
      const dynamicConfig = this.adjustThresholds(this.config, performance);
      return new SequentialShortCircuitStrategy(dynamicConfig).execute(query, sources);
    }
  }

  private rankSources(
    sources: Map<string, InformationSource>,
    performance: SourcePerformance
  ): InformationSource[] {
    return Array.from(sources.values()).sort((a, b) => {
      const scoreA = performance[a.name]?.successRate || 0.5;
      const scoreB = performance[b.name]?.successRate || 0.5;
      return scoreB - scoreA;
    });
  }

  private shouldParallelize(performance: SourcePerformance, query: Query): boolean {
    // Parallelize if:
    // 1. No single source dominates (best success rate < 80%)
    // 2. Query is complex
    // 3. Multiple sources have good historical performance

    const bestSuccessRate = Math.max(...Object.values(performance).map(p => p.successRate));
    const goodSources = Object.values(performance).filter(p => p.successRate > 0.6).length;
    const complexity = estimateComplexity(query);

    return bestSuccessRate < 0.8 && goodSources > 2 && complexity > 5;
  }

  private adjustThresholds(
    config: StrategyConfig,
    performance: SourcePerformance
  ): StrategyConfig {
    // Lower thresholds for sources with high accuracy
    // Raise thresholds for sources with low accuracy

    const adjusted = { ...config };
    adjusted.sources = config.sources.map(sc => {
      const accuracy = performance[sc.name]?.accuracy || 0.8;
      return {
        ...sc,
        threshold: Math.round(sc.threshold * (0.5 + 0.5 * accuracy))
      };
    });

    return adjusted;
  }
}

// ============================================================================
// SOURCE IMPLEMENTATIONS
// ============================================================================

class KnowledgeBaseSource implements InformationSource {
  name = 'kb';
  priority = 1;

  constructor(private db: Database) {}

  async query(request: QueryRequest): Promise<SourceResult> {
    const start = Date.now();

    // Full-text search
    const results = await this.db.search(request.query.text);

    if (results.length === 0) {
      return {
        source: this.name,
        confidence: 0,
        answer: '',
        metadata: { latency: Date.now() - start, documentsSearched: 0 },
        reasoning: 'No matching documents found'
      };
    }

    const bestMatch = results[0];
    const confidence = this.calculateConfidence(bestMatch, results);

    return {
      source: this.name,
      confidence,
      answer: bestMatch.content,
      metadata: {
        latency: Date.now() - start,
        documentsSearched: results.length,
        matchScore: bestMatch.score,
        documentId: bestMatch.id
      },
      reasoning: `Found ${results.length} matching documents, best match score: ${bestMatch.score}`
    };
  }

  calculateConfidence(bestMatch: any, allResults: any[]): number {
    let confidence = bestMatch.score * 100;  // Assume 0-1 score

    // Exact title match: very high confidence
    if (bestMatch.exactTitleMatch) {
      confidence = Math.min(100, confidence * 1.3);
    }

    // Freshness factor
    const daysSinceUpdate = (Date.now() - bestMatch.updatedAt) / (1000 * 60 * 60 * 24);
    const freshness = Math.exp(-daysSinceUpdate / 180);  // 6-month half-life
    confidence *= (0.7 + 0.3 * freshness);

    // Multiple confirming documents
    if (allResults.length > 1 && allResults[1].score > 0.8) {
      confidence = Math.min(100, confidence * 1.1);
    }

    return Math.round(confidence);
  }

  canHandle(query: Query): boolean {
    // KB can handle all queries
    return true;
  }

  getAverageLatency(): number {
    return 200;  // Fast - local database
  }

  getCost(): number {
    return 0;  // Free - local resource
  }
}

class LLMSource implements InformationSource {
  name = 'llm';
  priority = 2;

  constructor(private client: AnthropicClient) {}

  async query(request: QueryRequest): Promise<SourceResult> {
    const start = Date.now();

    const response = await this.client.messages.create({
      model: 'claude-3-5-sonnet-20241022',
      max_tokens: 1024,
      messages: [{
        role: 'user',
        content: this.buildPrompt(request.query)
      }]
    });

    const answer = response.content[0].text;
    const confidence = this.calculateConfidence(answer, response);

    return {
      source: this.name,
      confidence,
      answer,
      metadata: {
        latency: Date.now() - start,
        tokensUsed: response.usage.total_tokens,
        model: response.model
      },
      reasoning: this.extractReasoning(answer)
    };
  }

  private buildPrompt(query: Query): string {
    return `Answer the following question. If you're unsure, say so clearly.

Question: ${query.text}

Provide:
1. Your answer
2. Your confidence level (0-100%)
3. Your reasoning

Format:
ANSWER: <your answer>
CONFIDENCE: <0-100>
REASONING: <why you're confident/unsure>`;
  }

  calculateConfidence(answer: string, response: any): number {
    // Extract self-reported confidence if provided
    const confidenceMatch = answer.match(/CONFIDENCE:\s*(\d+)/i);
    let confidence = confidenceMatch ? parseInt(confidenceMatch[1]) : 60;

    // Detect hedging language
    const hedgePhrases = [
      'might', 'possibly', 'unsure', 'probably', 'maybe',
      'i think', 'not certain', 'unclear', 'depends'
    ];
    const hasHedging = hedgePhrases.some(phrase =>
      answer.toLowerCase().includes(phrase)
    );
    if (hasHedging) {
      confidence *= 0.85;
    }

    // Detect definitiveness
    const definitivePhrases = ['definitely', 'certainly', 'always', 'never'];
    const isDefinitive = definitivePhrases.some(phrase =>
      answer.toLowerCase().includes(phrase)
    );
    if (isDefinitive) {
      confidence = Math.min(100, confidence * 1.1);
    }

    // Answer length heuristic
    const answerLength = answer.length;
    if (answerLength < 50) {
      confidence *= 0.9;  // Too short might be uncertain
    } else if (answerLength > 2000) {
      confidence *= 0.95;  // Too long might be rambling
    }

    return Math.round(confidence);
  }

  private extractReasoning(answer: string): string {
    const reasoningMatch = answer.match(/REASONING:\s*(.+)/is);
    return reasoningMatch ? reasoningMatch[1].trim() : 'LLM response';
  }

  canHandle(query: Query): boolean {
    return true;  // LLM can attempt any query
  }

  getAverageLatency(): number {
    return 2000;  // ~2 seconds for API call
  }

  getCost(): number {
    return 0.015;  // $0.015 per query (approximate)
  }
}

// Similar implementations for MSDocsSource and WebSearchSource...

// ============================================================================
// METRICS AND ANALYTICS
// ============================================================================

class MetricsCollector {
  private db: Database;

  async recordQuery(query: Query, response: Response, results: SourceResult[]) {
    await this.db.insert('query_metrics', {
      query_text: query.text,
      query_type: query.type,
      selected_source: response.source,
      final_confidence: response.confidence,
      sources_queried: results.map(r => r.source),
      total_latency: results.reduce((sum, r) => sum + r.metadata.latency, 0),
      timestamp: new Date()
    });
  }

  async getSourceAccuracy(): Promise<Record<string, number>> {
    // Calculate accuracy based on user feedback
    const results = await this.db.query(`
      SELECT
        source,
        AVG(CASE WHEN user_rating >= 4 THEN 1 ELSE 0 END) as accuracy
      FROM query_metrics
      WHERE user_rating IS NOT NULL
      AND timestamp > NOW() - INTERVAL '30 days'
      GROUP BY source
    `);

    return Object.fromEntries(results.map(r => [r.source, r.accuracy]));
  }

  async getPerformance(queryType: string): Promise<SourcePerformance> {
    const results = await this.db.query(`
      SELECT
        source,
        AVG(final_confidence) as avg_confidence,
        AVG(CASE WHEN final_confidence >= 75 THEN 1 ELSE 0 END) as success_rate,
        AVG(latency) as avg_latency
      FROM query_metrics
      WHERE query_type = $1
      AND timestamp > NOW() - INTERVAL '30 days'
      GROUP BY source
    `, [queryType]);

    return Object.fromEntries(
      results.map(r => [r.source, {
        avgConfidence: r.avg_confidence,
        successRate: r.success_rate,
        avgLatency: r.avg_latency
      }])
    );
  }
}

// ============================================================================
// USAGE EXAMPLE
// ============================================================================

async function main() {
  // Initialize orchestrator
  const orchestrator = new ConfidenceBasedOrchestrator({
    sources: [
      new KnowledgeBaseSource(db),
      new LLMSource(anthropicClient),
      new MSDocsSource(msDocsClient),
      new WebSearchSource(searchClient)
    ],
    config: loadConfig('routing-config.yaml'),
    cache: new RedisCache(redisClient),
    metrics: new MetricsCollector(db)
  });

  // Route query
  const query = {
    text: "How do I configure Azure AD authentication in my Node.js app?",
    userId: "user123",
    context: {
      previousQueries: [],
      userExpertiseLevel: "intermediate"
    }
  };

  const response = await orchestrator.route(query);

  console.log('Answer:', response.answer);
  console.log('Confidence:', response.confidence + '%');
  console.log('Source:', response.source);
  console.log('Reasoning:', response.reasoning);

  if (response.alternatives) {
    console.log('\nAlternative answers:');
    response.alternatives.forEach(alt => {
      console.log(`  ${alt.source} (${alt.confidence}%): ${alt.answer.substring(0, 100)}...`);
    });
  }
}
```

---

## 10. Monitoring and Observability

### 10.1 Key Metrics

```typescript
interface SystemMetrics {
  // Performance metrics
  averageLatency: number;
  p95Latency: number;
  p99Latency: number;

  // Accuracy metrics
  averageConfidence: number;
  confidenceDistribution: { range: string, count: number }[];
  userSatisfactionRate: number;

  // Source metrics
  sourceUsage: { source: string, percentage: number }[];
  sourceSuccessRate: { source: string, rate: number }[];

  // Cost metrics
  totalCost: number;
  costPerQuery: number;
  costBySource: { source: string, cost: number }[];

  // Cache metrics
  cacheHitRate: number;
  cacheSize: number;
}
```

### 10.2 Alerting Rules

```yaml
alerts:
  - name: HighLatency
    condition: p95_latency > 5000
    severity: warning

  - name: LowConfidence
    condition: avg_confidence < 70
    severity: warning

  - name: SourceFailure
    condition: source_error_rate > 0.1
    severity: critical

  - name: LowCacheHitRate
    condition: cache_hit_rate < 0.5
    severity: info
```

---

## 11. Testing Strategy

### 11.1 Unit Tests

Test individual components:
- Confidence calculation functions
- Source query logic
- Aggregation algorithms
- Edge case handlers

### 11.2 Integration Tests

Test source interactions:
- Mock source responses
- Test routing strategies
- Verify cache behavior
- Test failure recovery

### 11.3 Load Tests

Simulate production traffic:
- Concurrent query handling
- Cache performance under load
- Source timeout behavior
- Cost under scale

### 11.4 Confidence Calibration Tests

Validate confidence accuracy:
- Compare confidence scores to user feedback
- Test calibration across sources
- Validate edge case handling

---

## 12. Deployment Considerations

### 12.1 Infrastructure

```yaml
# docker-compose.yml
services:
  orchestrator:
    image: confidence-router:latest
    replicas: 3
    environment:
      - REDIS_URL=redis://cache:6379
      - POSTGRES_URL=postgres://db:5432
    depends_on:
      - cache
      - db

  cache:
    image: redis:7-alpine
    volumes:
      - redis-data:/data

  db:
    image: postgres:15
    volumes:
      - postgres-data:/var/lib/postgresql/data
```

### 12.2 Scaling Strategy

- **Horizontal scaling**: Add more orchestrator instances
- **Cache scaling**: Redis cluster for high availability
- **Database scaling**: Read replicas for analytics queries
- **Source connection pooling**: Reuse connections across instances

### 12.3 Cost Optimization

```typescript
class CostOptimizer {
  async optimizeRouting(query: Query): Promise<RoutingStrategy> {
    // If query is in cache, use free cache
    if (await this.cache.has(query)) {
      return null;  // Skip routing
    }

    // For simple queries, use cheap sources first
    if (query.complexity < 5) {
      return new SequentialStrategy(['kb', 'web']);  // Skip expensive LLM
    }

    // For complex queries, use LLM but with caching
    return new SequentialStrategy(['kb', 'llm']);
  }
}
```

---

## 13. Future Enhancements

### 13.1 Machine Learning Integration

- Train ML model to predict best source for query type
- Learn optimal confidence thresholds
- Predict query answerable probability per source

### 13.2 Multi-Modal Support

- Image-based queries
- Voice queries with transcription confidence
- Video content analysis

### 13.3 Federated Learning

- Learn from multiple tenants without sharing data
- Improve confidence calibration across deployments

### 13.4 Explainable AI

- Detailed reasoning for source selection
- Confidence breakdown by feature
- User-facing explanations

---

## Conclusion

This architecture provides a robust, scalable, and extensible foundation for confidence-based information source routing. Key strengths:

1. **Flexibility**: Multiple routing strategies for different scenarios
2. **Intelligence**: Adaptive learning from historical data
3. **Performance**: Multi-layer caching and parallel execution
4. **Extensibility**: Plugin architecture for new sources
5. **Reliability**: Comprehensive edge case handling
6. **Observability**: Rich metrics and monitoring

The system balances accuracy, latency, and cost while remaining maintainable and easy to extend. The confidence scoring framework provides a unified language for comparing disparate information sources, and the orchestrator intelligently routes queries based on patterns learned from production traffic.
