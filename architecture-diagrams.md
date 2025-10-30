# Confidence-Based Routing: Visual Architecture Diagrams

## 1. System Overview - High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                          CLIENT APPLICATION                              │
│                     (Web UI, CLI, API Client)                            │
└────────────────────────────────┬────────────────────────────────────────┘
                                 │
                                 │ HTTP/REST
                                 ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                          API GATEWAY                                     │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐                 │
│  │ Rate Limit   │  │ Auth/AuthZ   │  │  Validation  │                 │
│  └──────────────┘  └──────────────┘  └──────────────┘                 │
└────────────────────────────────┬────────────────────────────────────────┘
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                     ORCHESTRATION LAYER                                  │
│  ┌───────────────────────────────────────────────────────────────────┐ │
│  │              CONFIDENCE-BASED ORCHESTRATOR                        │ │
│  │  ┌─────────────┐ ┌──────────────┐ ┌──────────────┐              │ │
│  │  │   Query     │ │   Strategy   │ │ Confidence   │              │ │
│  │  │ Classifier  │ │   Selector   │ │  Aggregator  │              │ │
│  │  └─────────────┘ └──────────────┘ └──────────────┘              │ │
│  │                                                                    │ │
│  │  ┌─────────────┐ ┌──────────────┐ ┌──────────────┐              │ │
│  │  │  Routing    │ │   Conflict   │ │    Edge      │              │ │
│  │  │  Executor   │ │   Resolver   │ │    Case      │              │ │
│  │  └─────────────┘ └──────────────┘ └──────────────┘              │ │
│  └───────────────────────────────────────────────────────────────────┘ │
└────────────┬──────────────┬──────────────┬──────────────┬─────────────┘
             │              │              │              │
    ┌────────▼───┐   ┌──────▼──────┐  ┌───▼──────┐  ┌───▼───────┐
    │  KB Source │   │  LLM Source │  │ MS Docs  │  │    Web    │
    │            │   │             │  │  Source  │  │  Search   │
    │  • Local   │   │  • Claude   │  │          │  │           │
    │  • Fast    │   │  • GPT      │  │  • API   │  │  • Bing   │
    │  • Free    │   │  • Gemini   │  │  • REST  │  │  • Google │
    └────────┬───┘   └──────┬──────┘  └───┬──────┘  └───┬───────┘
             │              │              │              │
             └──────────────┴──────────────┴──────────────┘
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                         DATA & CACHE LAYER                               │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐                 │
│  │   Redis      │  │  PostgreSQL  │  │  Analytics   │                 │
│  │   Cache      │  │   Metrics    │  │   Engine     │                 │
│  │   (L1/L2)    │  │   History    │  │              │                 │
│  └──────────────┘  └──────────────┘  └──────────────┘                 │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Query Routing Decision Flow

```
                              ┌─────────────┐
                              │   QUERY     │
                              │   ARRIVES   │
                              └──────┬──────┘
                                     │
                                     ▼
                        ┌────────────────────────┐
                        │   Check Cache          │
                        │   (Redis L1 + L2)      │
                        └──────┬─────────────────┘
                               │
                    ┌──────────┴──────────┐
                    │                     │
                 FOUND                NOT FOUND
                    │                     │
                    ▼                     ▼
            ┌──────────────┐    ┌────────────────────┐
            │   Return     │    │  Query Classifier  │
            │   Cached     │    │                    │
            │   Result     │    │  • Complexity      │
            └──────────────┘    │  • Urgency         │
                                │  • Domain          │
                                │  • Type            │
                                └─────────┬──────────┘
                                          │
                                          ▼
                              ┌───────────────────────┐
                              │  Select Strategy      │
                              │                       │
                              │  • Sequential         │
                              │  • Parallel           │
                              │  • Adaptive           │
                              └───┬───────┬───────┬───┘
                                  │       │       │
                    ┌─────────────┘       │       └─────────────┐
                    │                     │                     │
                    ▼                     ▼                     ▼
        ┌───────────────────┐ ┌──────────────────┐ ┌──────────────────┐
        │   Sequential      │ │   Parallel       │ │   Adaptive       │
        │   Short-Circuit   │ │   Aggregate      │ │   Learning       │
        └────────┬──────────┘ └────────┬─────────┘ └────────┬─────────┘
                 │                     │                     │
                 └──────────┬──────────┴──────────┬──────────┘
                            │                     │
                            ▼                     ▼
                  ┌──────────────────┐   ┌──────────────────┐
                  │  Execute Sources │   │  Execute Sources │
                  │   (Sequential)   │   │   (Parallel)     │
                  └────────┬─────────┘   └────────┬─────────┘
                           │                      │
                           └──────────┬───────────┘
                                      │
                                      ▼
                        ┌──────────────────────────┐
                        │  Confidence Aggregation  │
                        │                          │
                        │  • Normalize scores      │
                        │  • Sort by confidence    │
                        │  • Detect conflicts      │
                        │  • Handle edge cases     │
                        └──────────┬───────────────┘
                                   │
                    ┌──────────────┴───────────────┐
                    │                              │
                HIGH CONFIDENCE              LOW CONFIDENCE
               (>= 70%)                         (< 70%)
                    │                              │
                    ▼                              ▼
        ┌───────────────────┐          ┌──────────────────┐
        │  Check Conflicts  │          │  Low Confidence  │
        │                   │          │     Handler      │
        └────┬──────────┬───┘          │                  │
             │          │              │  • Return best   │
        NO CONFLICT  CONFLICT          │  • Add warning   │
             │          │              │  • Show alts     │
             ▼          ▼              └──────────────────┘
    ┌────────────┐  ┌──────────┐
    │   Build    │  │ Conflict │
    │  Response  │  │ Resolver │
    └──────┬─────┘  └────┬─────┘
           │             │
           └──────┬──────┘
                  │
                  ▼
        ┌──────────────────┐
        │   Cache Result   │
        │   (with TTL)     │
        └────────┬─────────┘
                 │
                 ▼
        ┌──────────────────┐
        │  Record Metrics  │
        └────────┬─────────┘
                 │
                 ▼
        ┌──────────────────┐
        │  Return Response │
        └──────────────────┘
```

---

## 3. Sequential Short-Circuit Strategy (Detailed)

```
┌─────────────┐
│   START     │
│   Query     │
└──────┬──────┘
       │
       ▼
┌──────────────────────────────────────────┐
│  SOURCE 1: Knowledge Base                │
│  Threshold: 85%                          │
│  Timeout: 500ms                          │
└──────┬───────────────────────────────────┘
       │
       ├─ Search local KB
       ├─ Calculate confidence
       │
       ▼
    Confidence?
       │
       ├─── ≥ 85% ────► ┌──────────────┐
       │                │  RETURN      │─► Success (Fast Path)
       │                │  KB Result   │
       │                └──────────────┘
       │
       ├─── < 85% ────► Continue
       │
       ▼
┌──────────────────────────────────────────┐
│  SOURCE 2: LLM (Claude/GPT)              │
│  Threshold: 75%                          │
│  Timeout: 3000ms                         │
└──────┬───────────────────────────────────┘
       │
       ├─ Call LLM API
       ├─ Parse response
       ├─ Calculate confidence
       │
       ▼
    Confidence?
       │
       ├─── ≥ 75% ────► ┌──────────────┐
       │                │  RETURN      │─► Success (Medium Path)
       │                │  LLM Result  │
       │                └──────────────┘
       │
       ├─── < 75% ────► Continue
       │
       ▼
┌──────────────────────────────────────────┐
│  SOURCE 3: MS Docs                       │
│  Threshold: 70%                          │
│  Timeout: 2000ms                         │
└──────┬───────────────────────────────────┘
       │
       ├─ Search docs API
       ├─ Filter by relevance
       ├─ Calculate confidence
       │
       ▼
    Confidence?
       │
       ├─── ≥ 70% ────► ┌──────────────┐
       │                │  RETURN      │─► Success
       │                │  Doc Result  │
       │                └──────────────┘
       │
       ├─── < 70% ────► Continue
       │
       ▼
┌──────────────────────────────────────────┐
│  SOURCE 4: Web Search                    │
│  Threshold: None (last resort)           │
│  Timeout: 5000ms                         │
└──────┬───────────────────────────────────┘
       │
       ├─ Search web
       ├─ Parse results
       ├─ Calculate confidence
       │
       ▼
┌──────────────────────────────────────────┐
│  RETURN                                  │
│  Best Available Result                   │
│  (May have low confidence warning)       │
└──────────────────────────────────────────┘

Latency Analysis:
─────────────────
Best Case:  KB hit at 85%+       →  ~100-500ms
Good Case:  LLM hit at 75%+      →  ~1500-3500ms
Fair Case:  Docs hit at 70%+     →  ~3500-5500ms
Worst Case: Web search required  →  ~5500-10000ms
```

---

## 4. Parallel Aggregate Strategy (Detailed)

```
                        ┌─────────────┐
                        │   START     │
                        │   Query     │
                        └──────┬──────┘
                               │
                    ┌──────────┴──────────┐
                    │  Launch All Sources │
                    │    In Parallel      │
                    └──────────┬──────────┘
                               │
        ┌──────────────────────┼──────────────────────┐
        │                      │                      │
        ▼                      ▼                      ▼
┌───────────────┐      ┌───────────────┐      ┌───────────────┐
│  KB Source    │      │  LLM Source   │      │  MS Docs      │
│               │      │               │      │               │
│  Start: 0ms   │      │  Start: 0ms   │      │  Start: 0ms   │
│  Timeout: 1s  │      │  Timeout: 3s  │      │  Timeout: 2s  │
└───────┬───────┘      └───────┬───────┘      └───────┬───────┘
        │                      │                      │
        │  100ms               │  2000ms              │  1800ms
        ▼                      ▼                      ▼
    ┌───────┐              ┌───────┐              ┌───────┐
    │ Done  │              │ Done  │              │ Done  │
    │ 90%   │              │ 80%   │              │ 75%   │
    └───────┘              └───────┘              └───────┘
        │                      │                      │
        └──────────────────────┼──────────────────────┘
                               │
                               ▼
                    ┌──────────────────────┐
                    │  Wait for Web Search │
                    │    (if configured)   │
                    └──────────┬───────────┘
                               │  4000ms
                               ▼
                          ┌────────┐
                          │ Done   │
                          │ 65%    │
                          └────┬───┘
                               │
                               ▼
                    ┌──────────────────────┐
                    │  All Results Ready   │
                    │                      │
                    │  KB:      90%        │
                    │  LLM:     80%        │
                    │  MS Docs: 75%        │
                    │  Web:     65%        │
                    └──────────┬───────────┘
                               │
                               ▼
                    ┌──────────────────────┐
                    │  Normalize Scores    │
                    │  (Apply Historical   │
                    │   Accuracy Factors)  │
                    └──────────┬───────────┘
                               │
                               ▼
                    ┌──────────────────────┐
                    │  Calibrated Scores:  │
                    │                      │
                    │  KB:      90% x 0.85 │ = 76.5%
                    │  LLM:     80% x 0.80 │ = 64.0%
                    │  MS Docs: 75% x 0.82 │ = 61.5%
                    │  Web:     65% x 0.70 │ = 45.5%
                    └──────────┬───────────┘
                               │
                               ▼
                    ┌──────────────────────┐
                    │  Detect Conflicts    │
                    └──────────┬───────────┘
                               │
                    ┌──────────┴──────────┐
                    │                     │
              NO CONFLICT           CONFLICT
                    │                     │
                    ▼                     ▼
        ┌───────────────────┐   ┌──────────────────┐
        │  Select Best      │   │  Apply Tie-      │
        │  (KB: 76.5%)      │   │  Breaker Rules   │
        └────────┬──────────┘   └────────┬─────────┘
                 │                       │
                 └───────────┬───────────┘
                             │
                             ▼
                  ┌──────────────────────┐
                  │  Return Response     │
                  │  + Alternatives      │
                  └──────────────────────┘

Total Latency: 4000ms (slowest source)
Reliability: High (fault-tolerant)
Cost: High (all APIs called)
Best For: Critical queries requiring best possible answer
```

---

## 5. Confidence Score Calibration Pipeline

```
┌──────────────────────────────────────────────────────────────────┐
│                    RAW SOURCE RESPONSE                           │
└─────────────────────────────┬────────────────────────────────────┘
                              │
                              ▼
                  ┌───────────────────────┐
                  │  SOURCE-SPECIFIC      │
                  │  CONFIDENCE           │
                  │  CALCULATOR           │
                  └───────┬───────────────┘
                          │
        ┌─────────────────┼─────────────────┐
        │                 │                 │
        ▼                 ▼                 ▼
  ┌──────────┐     ┌──────────┐     ┌──────────┐
  │    KB    │     │   LLM    │     │   WEB    │
  │          │     │          │     │          │
  │ • Match  │     │ • Model  │     │ • Rank   │
  │   score  │     │   conf.  │     │   score  │
  │ • Fresh  │     │ • Hedge  │     │ • Domain │
  │   ness   │     │   detect │     │   auth   │
  └────┬─────┘     └────┬─────┘     └────┬─────┘
       │                │                │
       └────────────────┼────────────────┘
                        │
                        ▼
            ┌───────────────────────┐
            │  BASE CONFIDENCE      │
            │  (0-100%)             │
            └───────────┬───────────┘
                        │
                        ▼
            ┌───────────────────────┐
            │  FEATURE ADJUSTMENTS  │
            │                       │
            │  • Answer length      │
            │  • Citations          │
            │  • Structure quality  │
            │  • Domain match       │
            └───────────┬───────────┘
                        │
                        ▼
            ┌───────────────────────┐
            │  HISTORICAL           │
            │  ACCURACY FACTOR      │
            │                       │
            │  Based on user        │
            │  feedback over time   │
            └───────────┬───────────┘
                        │
                        ▼
            ┌───────────────────────┐
            │  NORMALIZED           │
            │  CONFIDENCE           │
            │  (Calibrated 0-100%)  │
            └───────────┬───────────┘
                        │
                        ▼
            ┌───────────────────────┐
            │  FINAL CONFIDENCE     │
            │  SCORE                │
            └───────────────────────┘


Example Calculation:
────────────────────

KB Source:
  Raw match score:        0.92  (92%)
  Freshness factor:       0.85  (85%)
  Multiple docs boost:    1.10  (10% boost)
  Base confidence:        92% × 0.85 × 1.10 = 86%
  Historical accuracy:    0.85
  Final confidence:       86% × 0.85 = 73%

LLM Source:
  Model confidence:       75%
  Has hedging:            Yes (-20%)
  Has citations:          Yes (+20%)
  Good length:            Yes (no penalty)
  Base confidence:        75% × 0.8 × 1.2 = 72%
  Historical accuracy:    0.80
  Final confidence:       72% × 0.80 = 58%

Result: KB wins with 73% vs 58%
```

---

## 6. Edge Case Handling Decision Tree

```
                        ┌─────────────────┐
                        │  Results Ready  │
                        └────────┬────────┘
                                 │
                                 ▼
                        ┌─────────────────┐
                        │  Any results?   │
                        └────────┬────────┘
                                 │
                    ┌────────────┴────────────┐
                    │                         │
                   YES                        NO
                    │                         │
                    ▼                         ▼
        ┌───────────────────┐     ┌──────────────────┐
        │ Best confidence?  │     │  ALL SOURCES     │
        └────────┬──────────┘     │  FAILED          │
                 │                │                  │
     ┌───────────┼───────────┐   │  Return error    │
     │           │           │   │  + fallback      │
  < 50%      50-70%       > 70%  └──────────────────┘
     │           │           │
     ▼           ▼           ▼
┌─────────┐ ┌────────┐ ┌──────────┐
│  VERY   │ │  LOW   │ │  NORMAL  │
│  LOW    │ │  CONF  │ │  FLOW    │
└────┬────┘ └───┬────┘ └────┬─────┘
     │          │           │
     │          │           ▼
     │          │      ┌──────────────┐
     │          │      │ Check for    │
     │          │      │ conflicts    │
     │          │      └──────┬───────┘
     │          │             │
     │          │    ┌────────┴────────┐
     │          │    │                 │
     │          │   YES                NO
     │          │    │                 │
     │          │    ▼                 ▼
     │          │  ┌──────────┐  ┌──────────┐
     │          │  │ Conflict │  │  Select  │
     │          │  │ Resolver │  │   Best   │
     │          │  └──────────┘  └──────────┘
     │          │
     │          ▼
     │    ┌──────────────────┐
     │    │ LOW CONFIDENCE   │
     │    │ HANDLER          │
     │    │                  │
     │    │ • Return best    │
     │    │ • Add warning    │
     │    │ • Show alts      │
     │    │ • Suggest verify │
     │    └──────────────────┘
     │
     ▼
┌─────────────────────┐
│ VERY LOW CONFIDENCE │
│ HANDLER             │
│                     │
│ • Return best       │
│ • Strong warning    │
│ • All alternatives  │
│ • Suggest human     │
└─────────────────────┘


Conflict Resolution Decision Tree:
───────────────────────────────────

        ┌──────────────────┐
        │  Semantic        │
        │  Similarity      │
        └────────┬─────────┘
                 │
     ┌───────────┴───────────┐
     │                       │
  > 70%                   < 70%
  (Agree)                (Conflict)
     │                       │
     ▼                       ▼
┌──────────┐         ┌──────────────┐
│ Combine  │         │ Tie-Breakers │
│ Answers  │         └──────┬───────┘
└──────────┘                │
                            │
              ┌─────────────┼─────────────┐
              │             │             │
              ▼             ▼             ▼
        ┌─────────┐   ┌─────────┐   ┌─────────┐
        │ Recency │   │Authority│   │Specific │
        │  30%    │   │  40%    │   │  30%    │
        └─────────┘   └─────────┘   └─────────┘
              │             │             │
              └─────────────┼─────────────┘
                            │
                            ▼
                    ┌───────────────┐
                    │ Calculate     │
                    │ Weighted      │
                    │ Scores        │
                    └───────┬───────┘
                            │
                            ▼
                    ┌───────────────┐
                    │ Select Winner │
                    │ (reduce conf  │
                    │  by 10%)      │
                    └───────────────┘
```

---

## 7. Cache Strategy Layers

```
┌─────────────────────────────────────────────────────────────────┐
│                         QUERY ARRIVES                           │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
                  ┌──────────────────────┐
                  │   L1: In-Memory      │
                  │   (Redis / Local)    │
                  │                      │
                  │   TTL: 1-24 hours    │
                  │   Hit Rate: ~40%     │
                  │   Latency: 1-5ms     │
                  └──────────┬───────────┘
                             │
                    ┌────────┴────────┐
                    │                 │
                   HIT               MISS
                    │                 │
                    │                 ▼
                    │      ┌──────────────────────┐
                    │      │   L2: Distributed    │
                    │      │   (Redis Cluster)    │
                    │      │                      │
                    │      │   TTL: 7 days        │
                    │      │   Hit Rate: ~20%     │
                    │      │   Latency: 5-20ms    │
                    │      └──────────┬───────────┘
                    │                 │
                    │        ┌────────┴────────┐
                    │        │                 │
                    │       HIT               MISS
                    │        │                 │
                    │        │                 ▼
                    │        │      ┌──────────────────────┐
                    │        │      │   L3: Database       │
                    │        │      │   (PostgreSQL)       │
                    │        │      │                      │
                    │        │      │   Historical results│
                    │        │      │   Hit Rate: ~10%     │
                    │        │      │   Latency: 20-100ms  │
                    │        │      └──────────┬───────────┘
                    │        │                 │
                    │        │        ┌────────┴────────┐
                    │        │        │                 │
                    │        │       HIT               MISS
                    │        │        │                 │
                    ▼        ▼        ▼                 ▼
                ┌────────────────────────┐   ┌──────────────────┐
                │   Return Cached        │   │  Execute Full    │
                │   Response             │   │  Source Routing  │
                │                        │   │                  │
                │   Update access time   │   │  Then cache at   │
                │   Increment hit counter│   │  all levels      │
                └────────────────────────┘   └──────────────────┘


Cache Key Generation:
─────────────────────

    Query Text: "How to configure Azure AD?"
         │
         ▼
    Normalize: "how to configure azure ad"
         │
         ▼
    Remove punctuation: "how to configure azure ad"
         │
         ▼
    Generate hash: "a3f2e9d1c4b5"
         │
         ▼
    Add context: "query:a3f2e9d1c4b5:sources:kblw"
         │
         ▼
    Final Key: "query:a3f2e9d1c4b5:sources:kblw"


Cache TTL Strategy:
───────────────────

    Confidence Level    │   TTL
    ────────────────────┼────────────
    90-100%             │   24 hours
    80-89%              │   6 hours
    70-79%              │   2 hours
    60-69%              │   1 hour
    < 60%               │   30 min
```

---

## 8. Performance Optimization Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                     OPTIMIZATION LAYERS                         │
└─────────────────────────────────────────────────────────────────┘

Layer 1: Query Optimization
────────────────────────────
    ┌──────────────────┐
    │ Query arrives    │
    └────────┬─────────┘
             │
             ▼
    ┌──────────────────┐      ┌─────────────────────┐
    │ Normalize query  │─────▶│ Deduplication       │
    │ • Lowercase      │      │ • Merge similar     │
    │ • Trim spaces    │      │ • Batch processing  │
    │ • Remove punct   │      └─────────────────────┘
    └────────┬─────────┘
             │
             ▼
    ┌──────────────────┐
    │ Classification   │
    │ (cached)         │
    └──────────────────┘


Layer 2: Connection Pooling
────────────────────────────
    Source Connections
    ├─ KB:       [█ █ - - -]  2/5 active
    ├─ LLM:      [█ █ █ - -]  3/5 active
    ├─ MS Docs:  [█ - - - -]  1/5 active
    └─ Web:      [█ █ - - -]  2/5 active

    Pool Configuration:
    • Min connections: 2
    • Max connections: 10
    • Idle timeout: 30s
    • Acquire timeout: 5s


Layer 3: Parallel Execution
────────────────────────────
    ┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐
    │  KB    │ │  LLM   │ │  Docs  │ │  Web   │
    └───┬────┘ └───┬────┘ └───┬────┘ └───┬────┘
        │          │          │          │
        │          │          │          │
        └──────────┴──────────┴──────────┘
                   │
                   ▼
          [Thread Pool / Workers]
                   │
                   ▼
          [Results Aggregator]


Layer 4: Adaptive Timeouts
───────────────────────────
    Source Performance Tracking:

    KB Source:
    ├─ P50 latency: 80ms
    ├─ P95 latency: 150ms
    ├─ P99 latency: 300ms
    └─ Timeout: 360ms (P99 × 1.2)

    LLM Source:
    ├─ P50 latency: 1800ms
    ├─ P95 latency: 3200ms
    ├─ P99 latency: 5000ms
    └─ Timeout: 6000ms (P99 × 1.2)


Layer 5: Request Batching
──────────────────────────
    Similar Queries:
    ┌──────────────────────┐
    │ Query 1: Azure AD... │
    │ Query 2: Azure AD... │◄─── Grouped
    │ Query 3: Azure AD... │
    └──────────────────────┘
            │
            ▼
    ┌──────────────────────┐
    │ Single LLM Request   │
    │ with context         │
    └──────────────────────┘


Performance Metrics:
────────────────────

Without Optimization:
├─ Avg latency: 4500ms
├─ P95 latency: 8000ms
├─ Cache hit: 30%
├─ Cost per query: $0.02
└─ Throughput: 50 req/s

With Optimization:
├─ Avg latency: 800ms     (82% improvement)
├─ P95 latency: 2500ms    (69% improvement)
├─ Cache hit: 65%         (117% improvement)
├─ Cost per query: $0.007 (65% reduction)
└─ Throughput: 200 req/s  (300% improvement)
```

---

## 9. Monitoring and Observability

```
┌─────────────────────────────────────────────────────────────────┐
│                      MONITORING DASHBOARD                       │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────┬─────────────────────────────────┐
│  Real-Time Metrics          │  Performance Graphs              │
├─────────────────────────────┼─────────────────────────────────┤
│  Queries/sec:    125        │   Latency (P50/P95/P99)         │
│  Avg Latency:    850ms      │   ▲                             │
│  Avg Confidence: 78%        │100│    ▄▄                       │
│  Cache Hit Rate: 62%        │ 90│   █  █    ▄                 │
│  Error Rate:     0.2%       │ 80│  █    █  █ █                │
│                             │ 70│ █      ██   █               │
│  Active Sources:            │ 60│█            █▄▄             │
│  ✓ KB:      Healthy         │ 50│                █            │
│  ✓ LLM:     Healthy         │ 40└─────────────────▶           │
│  ✓ MS Docs: Healthy         │    0  4  8  12 16 20 (hrs)      │
│  ⚠ Web:     Degraded        │                                 │
└─────────────────────────────┴─────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│  Source Performance Breakdown                                   │
├───────────┬──────────┬────────────┬─────────┬─────────────────┤
│  Source   │ Queries  │ Avg Conf   │ Avg Lat │ Success Rate    │
├───────────┼──────────┼────────────┼─────────┼─────────────────┤
│  KB       │  45%     │ 82%        │  120ms  │ ████████░░  85% │
│  LLM      │  35%     │ 76%        │  1800ms │ ███████░░░  75% │
│  MS Docs  │  15%     │ 71%        │  1500ms │ ██████░░░░  65% │
│  Web      │  5%      │ 58%        │  3200ms │ ████░░░░░░  45% │
└───────────┴──────────┴────────────┴─────────┴─────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│  Confidence Distribution                                        │
├─────────────────────────────────────────────────────────────────┤
│  90-100%  ████████████████████         45%                      │
│  80-89%   ████████████                 25%                      │
│  70-79%   ████████                     18%                      │
│  60-69%   ████                         8%                       │
│  < 60%    ██                           4%                       │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│  Recent Alerts                                                  │
├─────────────────────────────────────────────────────────────────┤
│  🟡 WARNING: Web Search source latency high (4500ms)            │
│  🟢 INFO: Cache hit rate improved to 62%                        │
│  🟢 RESOLVED: LLM API timeout issues cleared                    │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│  Cost Analysis (Last 24h)                                       │
├─────────────────────────────────────────────────────────────────┤
│  Total Queries: 250,000                                         │
│  Total Cost: $1,250.00                                          │
│  Cost per Query: $0.005                                         │
│                                                                 │
│  Breakdown:                                                     │
│  ├─ KB:       $0          (0%)                                  │
│  ├─ LLM:      $875.00     (70%)                                 │
│  ├─ MS Docs:  $187.50     (15%)                                 │
│  └─ Web:      $187.50     (15%)                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## 10. System Scalability Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     PRODUCTION DEPLOYMENT                       │
└─────────────────────────────────────────────────────────────────┘

                         ┌──────────────┐
                         │   CDN /      │
                         │   Load       │
                         │   Balancer   │
                         └──────┬───────┘
                                │
                ┌───────────────┼───────────────┐
                │               │               │
                ▼               ▼               ▼
        ┌──────────────┐ ┌──────────────┐ ┌──────────────┐
        │ Orchestrator │ │ Orchestrator │ │ Orchestrator │
        │  Instance 1  │ │  Instance 2  │ │  Instance 3  │
        └──────┬───────┘ └──────┬───────┘ └──────┬───────┘
               │                │                │
               └────────────────┼────────────────┘
                                │
                ┌───────────────┴────────────────┐
                │                                │
                ▼                                ▼
        ┌──────────────┐                ┌──────────────┐
        │    Redis     │                │  PostgreSQL  │
        │   Cluster    │                │   Primary    │
        │              │                │              │
        │  ┌────────┐  │                │  ┌────────┐  │
        │  │ Master │  │                │  │ Master │  │
        │  └───┬────┘  │                │  └───┬────┘  │
        │      │       │                │      │       │
        │  ┌───┴────┐  │                │  ┌───┴────┐  │
        │  │ Slave1 │  │                │  │ Read   │  │
        │  └────────┘  │                │  │Replica │  │
        │  ┌────────┐  │                │  └────────┘  │
        │  │ Slave2 │  │                └──────────────┘
        │  └────────┘  │
        └──────────────┘


Scaling Strategy:
─────────────────

Horizontal Scaling (Orchestrator Instances):
├─ Auto-scale based on CPU > 70%
├─ Min instances: 3
├─ Max instances: 20
└─ Scale-up time: 60s

Cache Scaling (Redis):
├─ Cluster mode with 6 nodes
├─ Replication factor: 2
└─ Automatic failover

Database Scaling (PostgreSQL):
├─ Primary for writes
├─ 2 read replicas for analytics
└─ Connection pooling (PgBouncer)

Geographic Distribution:
────────────────────────
    US-East          US-West          EU-West
    ┌────────┐      ┌────────┐      ┌────────┐
    │ Region │      │ Region │      │ Region │
    │        │      │        │      │        │
    │ 3 inst │      │ 3 inst │      │ 2 inst │
    └────────┘      └────────┘      └────────┘
```

This comprehensive visual architecture documentation covers all major aspects of the confidence-based routing system, from high-level architecture to detailed decision flows and performance optimization strategies.
