/**
 * Confidence-Based Information Source Routing
 * Implementation Examples and Code Samples
 *
 * This file contains concrete, runnable examples of the architecture components.
 */

import { EventEmitter } from 'events';

// ============================================================================
// TYPE DEFINITIONS
// ============================================================================

interface Query {
  text: string;
  userId: string;
  context?: QueryContext;
  metadata?: Record<string, any>;
}

interface QueryContext {
  previousQueries?: string[];
  userExpertiseLevel?: 'beginner' | 'intermediate' | 'expert';
  urgency?: 'low' | 'normal' | 'high' | 'critical';
  domain?: string;
}

interface SourceResult {
  source: string;
  confidence: number;
  answer: string;
  metadata: {
    latency: number;
    cost?: number;
    tokensUsed?: number;
    documentsSearched?: number;
    [key: string]: any;
  };
  reasoning: string;
  originalConfidence?: number;
}

interface Response {
  answer: string;
  confidence: number;
  source: string;
  metadata: any;
  reasoning: string;
  alternatives?: Array<{
    answer: string;
    source: string;
    confidence: number;
  }>;
  warning?: string;
  conflict?: boolean;
  timestamp: string;
}

interface StrategyConfig {
  type: string;
  sources: Array<{
    name: string;
    threshold: number;
    timeout: number;
  }>;
  timeout?: number;
  minConfidence?: number;
}

interface SourcePerformance {
  [source: string]: {
    avgConfidence: number;
    successRate: number;
    avgLatency: number;
    accuracy?: number;
  };
}

// ============================================================================
// ABSTRACT INTERFACES
// ============================================================================

interface InformationSource {
  name: string;
  priority: number;

  query(request: { query: Query }): Promise<SourceResult>;
  calculateConfidence(result: any, context?: any): number;
  canHandle(query: Query): boolean;
  getAverageLatency(): number;
  getCost(): number;
}

interface RoutingStrategy {
  execute(query: Query, sources: Map<string, InformationSource>): Promise<SourceResult[]>;
}

interface CacheManager {
  get(query: Query): Promise<Response | null>;
  set(query: Query, response: Response, ttl: number): Promise<void>;
  invalidate(pattern: string): Promise<void>;
}

// ============================================================================
// MAIN ORCHESTRATOR IMPLEMENTATION
// ============================================================================

class ConfidenceBasedOrchestrator extends EventEmitter {
  private sources: Map<string, InformationSource>;
  private strategyFactory: StrategyFactory;
  private cache: CacheManager;
  private metrics: MetricsCollector;
  private config: any;

  constructor(options: {
    sources: InformationSource[];
    config: any;
    cache: CacheManager;
    metrics: MetricsCollector;
  }) {
    super();
    this.sources = new Map(options.sources.map(s => [s.name, s]));
    this.strategyFactory = new StrategyFactory(options.config);
    this.cache = options.cache;
    this.metrics = options.metrics;
    this.config = options.config;
  }

  async route(query: Query, options?: { bypassCache?: boolean }): Promise<Response> {
    const startTime = Date.now();
    this.emit('query:start', { query, timestamp: startTime });

    try {
      // Step 1: Check cache
      if (!options?.bypassCache) {
        const cached = await this.cache.get(query);
        if (cached) {
          this.metrics.recordCacheHit();
          this.emit('query:cache-hit', { query });
          return cached;
        }
      }

      // Step 2: Classify query
      const classification = await this.classifyQuery(query);
      this.emit('query:classified', { query, classification });

      // Step 3: Select routing strategy
      const strategy = this.strategyFactory.getStrategy(classification);
      this.emit('query:strategy-selected', { query, strategy: strategy.constructor.name });

      // Step 4: Execute strategy
      const results = await strategy.execute(query, this.sources);
      this.emit('query:sources-queried', { query, results });

      // Step 5: Aggregate results
      const response = await this.aggregateResults(results, query);
      this.emit('query:response-ready', { query, response });

      // Step 6: Cache response
      const ttl = this.calculateTTL(response.confidence);
      await this.cache.set(query, response, ttl);

      // Step 7: Record metrics
      const latency = Date.now() - startTime;
      await this.metrics.recordQuery(query, response, results, latency);

      return response;

    } catch (error) {
      this.emit('query:error', { query, error });
      throw error;
    }
  }

  private async classifyQuery(query: Query): Promise<QueryClassification> {
    const text = query.text.toLowerCase();

    // Detect urgency
    const urgentKeywords = ['urgent', 'critical', 'emergency', 'asap', 'immediately'];
    const urgency = urgentKeywords.some(kw => text.includes(kw)) ? 'critical' :
                   query.context?.urgency || 'normal';

    // Estimate complexity (0-10 scale)
    const complexity = this.estimateComplexity(query);

    // Detect domain
    const domain = this.detectDomain(query);

    // Detect query type
    const type = this.detectQueryType(query);

    return { urgency, complexity, domain, type };
  }

  private estimateComplexity(query: Query): number {
    const text = query.text;
    let complexity = 5; // Base complexity

    // Length factor
    const words = text.split(/\s+/).length;
    if (words > 50) complexity += 2;
    else if (words > 20) complexity += 1;
    else if (words < 5) complexity -= 1;

    // Question marks (multiple questions = complex)
    const questionCount = (text.match(/\?/g) || []).length;
    if (questionCount > 1) complexity += 1;

    // Technical terms
    const technicalTerms = ['architecture', 'implement', 'configure', 'optimize', 'debug'];
    const hasTechnicalTerms = technicalTerms.some(term =>
      text.toLowerCase().includes(term)
    );
    if (hasTechnicalTerms) complexity += 1;

    // Compound queries (and, or, also)
    const compounds = ['and also', ' or ', 'additionally', 'furthermore'];
    const hasCompounds = compounds.some(c => text.toLowerCase().includes(c));
    if (hasCompounds) complexity += 2;

    return Math.min(10, Math.max(1, complexity));
  }

  private detectDomain(query: Query): string {
    const text = query.text.toLowerCase();

    const domainKeywords = {
      azure: ['azure', 'az cli', 'resource group', 'subscription'],
      aws: ['aws', 's3', 'ec2', 'lambda'],
      kubernetes: ['kubernetes', 'k8s', 'kubectl', 'pod', 'deployment'],
      database: ['database', 'sql', 'query', 'postgres', 'mongodb'],
      networking: ['network', 'firewall', 'dns', 'load balancer'],
      security: ['security', 'auth', 'permission', 'certificate'],
    };

    for (const [domain, keywords] of Object.entries(domainKeywords)) {
      if (keywords.some(kw => text.includes(kw))) {
        return domain;
      }
    }

    return 'general';
  }

  private detectQueryType(query: Query): QueryType {
    const text = query.text.toLowerCase();

    // Factual: "what is", "who is", "when did"
    if (/^(what|who|when|where)\s+(is|are|was|were|did)/i.test(text)) {
      return 'factual';
    }

    // Procedural: "how to", "how do I", "steps to"
    if (/how\s+(to|do|can|should)|steps\s+to/i.test(text)) {
      return 'procedural';
    }

    // Diagnostic: "why is", "error", "not working", "troubleshoot"
    if (/why\s+(is|does|did)|error|not\s+working|troubleshoot|debug/i.test(text)) {
      return 'diagnostic';
    }

    // Comparative: "difference between", "compare", "vs"
    if (/difference\s+between|compare|versus|\svs\s/i.test(text)) {
      return 'comparative';
    }

    // Recommendation: "should I", "best practice", "recommend"
    if (/should\s+i|best\s+practice|recommend|suggest|advice/i.test(text)) {
      return 'recommendation';
    }

    return 'general';
  }

  private async aggregateResults(
    results: SourceResult[],
    query: Query
  ): Promise<Response> {
    // Filter valid results
    const valid = results.filter(r => r && r.confidence > 0);

    if (valid.length === 0) {
      return this.handleNoResults(query);
    }

    // Normalize confidence scores
    const normalized = await this.normalizeConfidence(valid);

    // Sort by confidence
    const sorted = normalized.sort((a, b) => b.confidence - a.confidence);

    // Handle edge cases
    if (sorted[0].confidence < 70) {
      return this.handleLowConfidence(sorted);
    }

    // Check for ties (within 5% confidence)
    if (sorted.length > 1 && Math.abs(sorted[0].confidence - sorted[1].confidence) < 5) {
      return this.handleConfidenceTie(sorted);
    }

    // Check for conflicts
    const topResults = sorted.slice(0, Math.min(3, sorted.length));
    const hasConflict = await this.detectConflict(topResults);
    if (hasConflict) {
      return this.handleConflict(topResults);
    }

    // Build final response
    return this.buildResponse(sorted[0], sorted.slice(1, 3));
  }

  private async normalizeConfidence(results: SourceResult[]): Promise<SourceResult[]> {
    // Get historical accuracy for each source
    const accuracy = await this.metrics.getSourceAccuracy();

    return results.map(result => ({
      ...result,
      originalConfidence: result.confidence,
      confidence: Math.round(result.confidence * (accuracy[result.source] || 0.8))
    }));
  }

  private async detectConflict(results: SourceResult[]): Promise<boolean> {
    if (results.length < 2) return false;

    // Check for semantic conflicts between high-confidence answers
    for (let i = 0; i < results.length - 1; i++) {
      for (let j = i + 1; j < results.length; j++) {
        const similarity = this.calculateSimilarity(
          results[i].answer,
          results[j].answer
        );

        // If both have high confidence but low similarity, it's a conflict
        if (results[i].confidence > 70 &&
            results[j].confidence > 70 &&
            similarity < 0.5) {
          return true;
        }
      }
    }

    return false;
  }

  private calculateSimilarity(text1: string, text2: string): number {
    // Simple word overlap similarity
    const words1 = new Set(text1.toLowerCase().split(/\s+/));
    const words2 = new Set(text2.toLowerCase().split(/\s+/));

    const intersection = new Set([...words1].filter(w => words2.has(w)));
    const union = new Set([...words1, ...words2]);

    return intersection.size / union.size;
  }

  private handleNoResults(query: Query): Response {
    return {
      answer: "I couldn't find a reliable answer to your question.",
      confidence: 0,
      source: 'none',
      metadata: {},
      reasoning: 'No sources returned results',
      warning: 'All information sources failed or returned no results',
      timestamp: new Date().toISOString()
    };
  }

  private handleLowConfidence(results: SourceResult[]): Response {
    const best = results[0];

    return {
      answer: best.answer,
      confidence: best.confidence,
      source: best.source,
      metadata: best.metadata,
      reasoning: best.reasoning,
      warning: 'Low confidence - please verify this answer independently',
      alternatives: results.slice(1, 3).map(r => ({
        answer: r.answer,
        source: r.source,
        confidence: r.confidence
      })),
      timestamp: new Date().toISOString()
    };
  }

  private handleConfidenceTie(results: SourceResult[]): Response {
    const topResults = results.filter(r =>
      r.confidence >= results[0].confidence - 5
    );

    if (topResults.length === 1) {
      return this.buildResponse(topResults[0], results.slice(1, 3));
    }

    // Check if answers agree
    const answersAgree = topResults.every((r, i) =>
      i === 0 || this.calculateSimilarity(r.answer, topResults[0].answer) > 0.7
    );

    if (answersAgree) {
      // Combine with weighted average
      return this.combineAnswers(topResults);
    } else {
      // Return primary with alternatives
      return {
        answer: topResults[0].answer,
        confidence: topResults[0].confidence,
        source: 'multiple',
        metadata: { sources: topResults.map(r => r.source) },
        reasoning: 'Multiple sources with similar confidence',
        alternatives: topResults.slice(1).map(r => ({
          answer: r.answer,
          source: r.source,
          confidence: r.confidence
        })),
        warning: 'Multiple high-confidence answers found - review alternatives',
        timestamp: new Date().toISOString()
      };
    }
  }

  private combineAnswers(results: SourceResult[]): Response {
    // Weight by confidence
    const totalConfidence = results.reduce((sum, r) => sum + r.confidence, 0);
    const weights = results.map(r => r.confidence / totalConfidence);

    // For now, just use the best answer but note multiple sources
    const best = results[0];
    const avgConfidence = totalConfidence / results.length;

    return {
      answer: best.answer,
      confidence: Math.round(avgConfidence),
      source: results.map(r => r.source).join(' + '),
      metadata: {
        sources: results.map(r => r.source),
        weights: weights
      },
      reasoning: `Combined answer from ${results.length} sources with similar confidence`,
      timestamp: new Date().toISOString()
    };
  }

  private handleConflict(results: SourceResult[]): Response {
    // Apply tie-breaker rules
    const tieBreakers = [
      { name: 'recency', weight: 0.3 },
      { name: 'authority', weight: 0.4 },
      { name: 'specificity', weight: 0.3 }
    ];

    const scores = results.map(result => {
      let score = result.confidence;

      // Recency bonus (if metadata has timestamp)
      if (result.metadata.timestamp) {
        const age = Date.now() - new Date(result.metadata.timestamp).getTime();
        const recencyBonus = Math.exp(-age / (1000 * 60 * 60 * 24 * 180)); // 6 month decay
        score += recencyBonus * 10 * tieBreakers[0].weight;
      }

      // Authority bonus (based on source priority)
      const source = this.sources.get(result.source);
      if (source) {
        const authorityBonus = (5 - source.priority) * 2; // Higher priority = lower number
        score += authorityBonus * tieBreakers[1].weight;
      }

      // Specificity bonus (longer, more detailed answers)
      const specificityBonus = Math.min(10, result.answer.length / 200);
      score += specificityBonus * tieBreakers[2].weight;

      return { result, score };
    });

    const winner = scores.sort((a, b) => b.score - a.score)[0].result;

    return {
      answer: winner.answer,
      confidence: Math.round(winner.confidence * 0.9), // Reduce confidence due to conflict
      source: winner.source,
      metadata: winner.metadata,
      reasoning: `Selected from conflicting answers using tie-breaker rules`,
      conflict: true,
      alternatives: results.filter(r => r !== winner).map(r => ({
        answer: r.answer,
        source: r.source,
        confidence: r.confidence
      })),
      warning: 'Multiple sources provided conflicting answers',
      timestamp: new Date().toISOString()
    };
  }

  private buildResponse(
    primary: SourceResult,
    alternatives: SourceResult[]
  ): Response {
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

  private calculateTTL(confidence: number): number {
    // Higher confidence = longer cache TTL
    if (confidence >= 90) return 3600 * 24; // 24 hours
    if (confidence >= 80) return 3600 * 6;  // 6 hours
    if (confidence >= 70) return 3600 * 2;  // 2 hours
    return 3600; // 1 hour
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
      if (!source) {
        console.warn(`Source ${sourceConfig.name} not found`);
        continue;
      }

      // Check if source can handle this query
      if (!source.canHandle(query)) {
        console.log(`Source ${sourceConfig.name} cannot handle query`);
        continue;
      }

      // Query with timeout
      const result = await this.queryWithTimeout(
        source,
        query,
        sourceConfig.timeout
      );

      if (result) {
        results.push(result);

        // Early termination if confidence threshold met
        if (result.confidence >= sourceConfig.threshold) {
          console.log(
            `Short-circuit: ${source.name} confidence ${result.confidence}% >= threshold ${sourceConfig.threshold}%`
          );
          break;
        } else {
          console.log(
            `Continue: ${source.name} confidence ${result.confidence}% < threshold ${sourceConfig.threshold}%`
          );
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
      const timeoutPromise = new Promise<never>((_, reject) =>
        setTimeout(() => reject(new Error('Timeout')), timeout)
      );

      const result = await Promise.race([
        source.query({ query }),
        timeoutPromise
      ]);

      return result;

    } catch (error: any) {
      console.error(`Source ${source.name} failed:`, error.message);
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
      .filter((r): r is PromiseFulfilledResult<SourceResult | null> =>
        r.status === 'fulfilled' && r.value !== null
      )
      .map(r => r.value!);
  }

  private async querySource(
    source: InformationSource,
    query: Query
  ): Promise<SourceResult | null> {
    const timeout = this.config.timeout || 5000;

    try {
      const timeoutPromise = new Promise<never>((_, reject) =>
        setTimeout(() => reject(new Error('Timeout')), timeout)
      );

      return await Promise.race([
        source.query({ query }),
        timeoutPromise
      ]);

    } catch (error: any) {
      console.error(`Source ${source.name} failed:`, error.message);
      return null;
    }
  }
}

class ParallelRaceStrategy implements RoutingStrategy {
  constructor(
    private config: StrategyConfig,
    private confidenceThreshold: number = 80
  ) {}

  async execute(
    query: Query,
    sources: Map<string, InformationSource>
  ): Promise<SourceResult[]> {
    const results: SourceResult[] = [];
    const sourceArray = Array.from(sources.values()).filter(s => s.canHandle(query));

    // Start all queries
    const promises = sourceArray.map(source =>
      this.querySource(source, query)
    );

    // Race to first high-confidence result
    for await (const result of this.iterateAsCompleted(promises)) {
      if (result) {
        results.push(result);

        // Stop if we found high-confidence answer
        if (result.confidence >= this.confidenceThreshold) {
          console.log(`Race winner: ${result.source} with ${result.confidence}% confidence`);
          break;
        }
      }
    }

    // If we exited early, wait a bit for other results to compare
    if (results.length > 0 && results[0].confidence >= this.confidenceThreshold) {
      await this.delay(500); // Give other sources 500ms to respond

      // Collect any additional results that came in
      const additionalResults = await Promise.race([
        Promise.allSettled(promises),
        this.delay(500)
      ]);

      if (Array.isArray(additionalResults)) {
        additionalResults
          .filter((r): r is PromiseFulfilledResult<SourceResult | null> =>
            r.status === 'fulfilled' && r.value !== null
          )
          .forEach(r => {
            if (!results.find(existing => existing.source === r.value!.source)) {
              results.push(r.value!);
            }
          });
      }
    }

    return results;
  }

  private async querySource(
    source: InformationSource,
    query: Query
  ): Promise<SourceResult | null> {
    const timeout = this.config.timeout || 5000;

    try {
      const timeoutPromise = new Promise<never>((_, reject) =>
        setTimeout(() => reject(new Error('Timeout')), timeout)
      );

      return await Promise.race([
        source.query({ query }),
        timeoutPromise
      ]);

    } catch (error: any) {
      console.error(`Source ${source.name} failed:`, error.message);
      return null;
    }
  }

  private async *iterateAsCompleted<T>(
    promises: Promise<T>[]
  ): AsyncGenerator<T, void, unknown> {
    const pending = [...promises];

    while (pending.length > 0) {
      const result = await Promise.race(
        pending.map((p, index) =>
          p.then(value => ({ value, index }))
        )
      );

      pending.splice(result.index, 1);
      yield result.value;
    }
  }

  private delay(ms: number): Promise<void> {
    return new Promise(resolve => setTimeout(resolve, ms));
  }
}

// ============================================================================
// STRATEGY FACTORY
// ============================================================================

class StrategyFactory {
  private strategies: Map<string, RoutingStrategy> = new Map();

  constructor(private config: any) {
    this.initializeStrategies();
  }

  private initializeStrategies() {
    // Initialize built-in strategies
    this.strategies.set(
      'sequential_short_circuit',
      new SequentialShortCircuitStrategy(this.config.strategies.default)
    );

    this.strategies.set(
      'parallel_aggregate',
      new ParallelAggregateStrategy(this.config.strategies.critical || this.config.strategies.default)
    );

    this.strategies.set(
      'parallel_race',
      new ParallelRaceStrategy(this.config.strategies.default, 80)
    );
  }

  getStrategy(classification: QueryClassification): RoutingStrategy {
    // Critical urgency: use parallel aggregate for best answer
    if (classification.urgency === 'critical') {
      return this.strategies.get('parallel_aggregate')!;
    }

    // Simple queries: use sequential short-circuit for speed
    if (classification.complexity < 5) {
      return this.strategies.get('sequential_short_circuit')!;
    }

    // Complex queries: use parallel race for balance
    if (classification.complexity > 7) {
      return this.strategies.get('parallel_race')!;
    }

    // Default: sequential short-circuit
    return this.strategies.get('sequential_short_circuit')!;
  }
}

// ============================================================================
// EXAMPLE SOURCE IMPLEMENTATIONS
// ============================================================================

class KnowledgeBaseSource implements InformationSource {
  name = 'kb';
  priority = 1;

  constructor(private documents: Array<{ title: string; content: string; timestamp: Date }>) {}

  async query(request: { query: Query }): Promise<SourceResult> {
    const start = Date.now();
    const queryText = request.query.text.toLowerCase();

    // Simple search: find documents with matching words
    const results = this.documents
      .map(doc => ({
        doc,
        score: this.calculateRelevance(queryText, doc)
      }))
      .filter(r => r.score > 0)
      .sort((a, b) => b.score - a.score);

    if (results.length === 0) {
      return {
        source: this.name,
        confidence: 0,
        answer: '',
        metadata: {
          latency: Date.now() - start,
          documentsSearched: this.documents.length
        },
        reasoning: 'No matching documents found in knowledge base'
      };
    }

    const best = results[0];
    const confidence = this.calculateConfidence(best, results);

    return {
      source: this.name,
      confidence,
      answer: best.doc.content,
      metadata: {
        latency: Date.now() - start,
        documentsSearched: this.documents.length,
        matchScore: best.score
      },
      reasoning: `Found ${results.length} matching documents, best match: "${best.doc.title}"`
    };
  }

  private calculateRelevance(query: string, doc: { title: string; content: string }): number {
    const queryWords = query.split(/\s+/);
    const docText = (doc.title + ' ' + doc.content).toLowerCase();

    const matches = queryWords.filter(word => docText.includes(word)).length;
    return matches / queryWords.length;
  }

  calculateConfidence(
    best: { doc: any; score: number },
    allResults: Array<{ doc: any; score: number }>
  ): number {
    let confidence = best.score * 100;

    // Title match: high confidence
    const queryWords = best.doc.title.toLowerCase().split(/\s+/);
    if (queryWords.length > 2) {
      confidence = Math.min(100, confidence * 1.3);
    }

    // Freshness factor
    const daysSinceUpdate = (Date.now() - best.doc.timestamp.getTime()) / (1000 * 60 * 60 * 24);
    const freshness = Math.exp(-daysSinceUpdate / 180); // 6-month half-life
    confidence *= (0.7 + 0.3 * freshness);

    // Multiple good matches: boost confidence
    if (allResults.length > 1 && allResults[1].score > 0.7) {
      confidence = Math.min(100, confidence * 1.1);
    }

    return Math.round(confidence);
  }

  canHandle(query: Query): boolean {
    return true; // KB can attempt all queries
  }

  getAverageLatency(): number {
    return 100; // Fast local search
  }

  getCost(): number {
    return 0; // Free local resource
  }
}

class MockLLMSource implements InformationSource {
  name = 'llm';
  priority = 2;

  async query(request: { query: Query }): Promise<SourceResult> {
    const start = Date.now();

    // Simulate API call latency
    await this.delay(1500 + Math.random() * 1000);

    // Mock response
    const answer = `Mock LLM answer for: ${request.query.text}. This is a simulated response that would come from a language model.`;
    const confidence = 70 + Math.random() * 20; // 70-90% confidence

    return {
      source: this.name,
      confidence: Math.round(confidence),
      answer,
      metadata: {
        latency: Date.now() - start,
        tokensUsed: 150
      },
      reasoning: 'LLM generated response based on training data'
    };
  }

  calculateConfidence(result: any): number {
    return 75; // Mock confidence
  }

  canHandle(query: Query): boolean {
    return true;
  }

  getAverageLatency(): number {
    return 2000;
  }

  getCost(): number {
    return 0.01;
  }

  private delay(ms: number): Promise<void> {
    return new Promise(resolve => setTimeout(resolve, ms));
  }
}

class MockWebSearchSource implements InformationSource {
  name = 'web';
  priority = 4;

  async query(request: { query: Query }): Promise<SourceResult> {
    const start = Date.now();

    // Simulate search latency
    await this.delay(2000 + Math.random() * 2000);

    const answer = `Mock web search result for: ${request.query.text}. Found information from various online sources.`;
    const confidence = 50 + Math.random() * 30; // 50-80% confidence

    return {
      source: this.name,
      confidence: Math.round(confidence),
      answer,
      metadata: {
        latency: Date.now() - start,
        resultsFound: 10
      },
      reasoning: 'Web search compiled results from multiple sources'
    };
  }

  calculateConfidence(result: any): number {
    return 60;
  }

  canHandle(query: Query): boolean {
    return true;
  }

  getAverageLatency(): number {
    return 3000;
  }

  getCost(): number {
    return 0.005;
  }

  private delay(ms: number): Promise<void> {
    return new Promise(resolve => setTimeout(resolve, ms));
  }
}

// ============================================================================
// CACHE IMPLEMENTATION
// ============================================================================

class InMemoryCacheManager implements CacheManager {
  private cache: Map<string, { response: Response; expiry: number }> = new Map();

  async get(query: Query): Promise<Response | null> {
    const key = this.generateKey(query);
    const cached = this.cache.get(key);

    if (!cached) {
      return null;
    }

    // Check expiry
    if (Date.now() > cached.expiry) {
      this.cache.delete(key);
      return null;
    }

    return cached.response;
  }

  async set(query: Query, response: Response, ttl: number): Promise<void> {
    const key = this.generateKey(query);
    this.cache.set(key, {
      response,
      expiry: Date.now() + (ttl * 1000)
    });
  }

  async invalidate(pattern: string): Promise<void> {
    const regex = new RegExp(pattern);
    for (const key of this.cache.keys()) {
      if (regex.test(key)) {
        this.cache.delete(key);
      }
    }
  }

  private generateKey(query: Query): string {
    // Normalize query text for better cache hits
    const normalized = query.text
      .toLowerCase()
      .trim()
      .replace(/\s+/g, ' ')
      .replace(/[^\w\s]/g, '');

    return `query:${this.hash(normalized)}`;
  }

  private hash(str: string): string {
    let hash = 0;
    for (let i = 0; i < str.length; i++) {
      const char = str.charCodeAt(i);
      hash = ((hash << 5) - hash) + char;
      hash = hash & hash; // Convert to 32-bit integer
    }
    return hash.toString(36);
  }
}

// ============================================================================
// METRICS COLLECTOR
// ============================================================================

class MetricsCollector {
  private queries: Array<{
    query: Query;
    response: Response;
    results: SourceResult[];
    latency: number;
    timestamp: Date;
  }> = [];

  private cacheHits = 0;
  private cacheMisses = 0;

  async recordQuery(
    query: Query,
    response: Response,
    results: SourceResult[],
    latency: number
  ): Promise<void> {
    this.queries.push({
      query,
      response,
      results,
      latency,
      timestamp: new Date()
    });

    this.cacheMisses++;

    // Keep only last 1000 queries
    if (this.queries.length > 1000) {
      this.queries.shift();
    }
  }

  recordCacheHit(): void {
    this.cacheHits++;
  }

  async getSourceAccuracy(): Promise<Record<string, number>> {
    // In production, this would query a database with user feedback
    // For now, return mock historical accuracy
    return {
      kb: 0.85,
      llm: 0.80,
      ms_docs: 0.82,
      web: 0.70
    };
  }

  async getPerformance(queryType: string): Promise<SourcePerformance> {
    // Calculate performance from recorded queries
    const relevantQueries = this.queries.filter(q =>
      this.classifyQueryType(q.query.text) === queryType
    );

    const sourceStats: Record<string, {
      confidences: number[];
      successes: number;
      total: number;
      latencies: number[];
    }> = {};

    for (const q of relevantQueries) {
      for (const result of q.results) {
        if (!sourceStats[result.source]) {
          sourceStats[result.source] = {
            confidences: [],
            successes: 0,
            total: 0,
            latencies: []
          };
        }

        sourceStats[result.source].confidences.push(result.confidence);
        sourceStats[result.source].latencies.push(result.metadata.latency);
        sourceStats[result.source].total++;
        if (result.confidence >= 75) {
          sourceStats[result.source].successes++;
        }
      }
    }

    const performance: SourcePerformance = {};
    for (const [source, stats] of Object.entries(sourceStats)) {
      performance[source] = {
        avgConfidence: stats.confidences.reduce((a, b) => a + b, 0) / stats.confidences.length,
        successRate: stats.successes / stats.total,
        avgLatency: stats.latencies.reduce((a, b) => a + b, 0) / stats.latencies.length
      };
    }

    return performance;
  }

  private classifyQueryType(text: string): string {
    text = text.toLowerCase();
    if (/^(what|who|when|where)\s+/i.test(text)) return 'factual';
    if (/how\s+to|how\s+do/i.test(text)) return 'procedural';
    if (/why|error|not\s+working/i.test(text)) return 'diagnostic';
    return 'general';
  }

  getStats() {
    const totalQueries = this.queries.length;
    const avgConfidence = this.queries.reduce((sum, q) => sum + q.response.confidence, 0) / totalQueries;
    const avgLatency = this.queries.reduce((sum, q) => sum + q.latency, 0) / totalQueries;
    const cacheHitRate = this.cacheHits / (this.cacheHits + this.cacheMisses);

    return {
      totalQueries,
      avgConfidence: Math.round(avgConfidence),
      avgLatency: Math.round(avgLatency),
      cacheHitRate: Math.round(cacheHitRate * 100),
      sourceUsage: this.getSourceUsage()
    };
  }

  private getSourceUsage(): Record<string, number> {
    const usage: Record<string, number> = {};

    for (const q of this.queries) {
      for (const result of q.results) {
        usage[result.source] = (usage[result.source] || 0) + 1;
      }
    }

    return usage;
  }
}

// ============================================================================
// TYPES
// ============================================================================

interface QueryClassification {
  urgency: 'low' | 'normal' | 'high' | 'critical';
  complexity: number;
  domain: string;
  type: QueryType;
}

type QueryType =
  | 'factual'
  | 'procedural'
  | 'diagnostic'
  | 'comparative'
  | 'recommendation'
  | 'general';

// ============================================================================
// EXAMPLE USAGE
// ============================================================================

async function demonstrateSystem() {
  console.log('='.repeat(80));
  console.log('Confidence-Based Information Source Routing - Demonstration');
  console.log('='.repeat(80));
  console.log();

  // Sample knowledge base
  const kbDocuments = [
    {
      title: 'Azure AD Authentication Setup',
      content: 'To configure Azure AD authentication in Node.js, install @azure/msal-node package and configure with your tenant ID and client ID. Use the ConfidentialClientApplication class to handle authentication flows.',
      timestamp: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000) // 30 days ago
    },
    {
      title: 'Node.js Best Practices',
      content: 'Follow these Node.js best practices: use async/await, handle errors properly, use environment variables, implement logging, and use connection pooling.',
      timestamp: new Date(Date.now() - 60 * 24 * 60 * 60 * 1000) // 60 days ago
    }
  ];

  // Initialize system
  const sources = [
    new KnowledgeBaseSource(kbDocuments),
    new MockLLMSource(),
    new MockWebSearchSource()
  ];

  const config = {
    strategies: {
      default: {
        type: 'sequential_short_circuit',
        sources: [
          { name: 'kb', threshold: 85, timeout: 1000 },
          { name: 'llm', threshold: 75, timeout: 3000 },
          { name: 'web', threshold: 0, timeout: 5000 }
        ]
      },
      critical: {
        type: 'parallel_aggregate',
        timeout: 5000
      }
    }
  };

  const cache = new InMemoryCacheManager();
  const metrics = new MetricsCollector();

  const orchestrator = new ConfidenceBasedOrchestrator({
    sources,
    config,
    cache,
    metrics
  });

  // Listen to events
  orchestrator.on('query:start', (data) => {
    console.log(`\n${'─'.repeat(80)}`);
    console.log(`🔍 Query started: "${data.query.text}"`);
  });

  orchestrator.on('query:classified', (data) => {
    console.log(`📊 Classification: ${JSON.stringify(data.classification)}`);
  });

  orchestrator.on('query:strategy-selected', (data) => {
    console.log(`🎯 Strategy: ${data.strategy}`);
  });

  orchestrator.on('query:sources-queried', (data) => {
    console.log(`📡 Sources queried: ${data.results.map((r: SourceResult) =>
      `${r.source}(${r.confidence}%)`
    ).join(', ')}`);
  });

  // Test queries
  const testQueries: Query[] = [
    {
      text: 'How do I configure Azure AD authentication in Node.js?',
      userId: 'user1',
      context: { userExpertiseLevel: 'intermediate' }
    },
    {
      text: 'What are the best practices for error handling in async functions?',
      userId: 'user2',
      context: { userExpertiseLevel: 'beginner' }
    },
    {
      text: 'URGENT: How to fix memory leak in production?',
      userId: 'user3',
      context: { urgency: 'critical', userExpertiseLevel: 'expert' }
    }
  ];

  for (const query of testQueries) {
    const response = await orchestrator.route(query);

    console.log(`\n✅ Response:`);
    console.log(`   Source: ${response.source}`);
    console.log(`   Confidence: ${response.confidence}%`);
    console.log(`   Answer: ${response.answer.substring(0, 150)}...`);

    if (response.warning) {
      console.log(`   ⚠️  Warning: ${response.warning}`);
    }

    if (response.conflict) {
      console.log(`   ⚔️  Conflict detected and resolved`);
    }

    if (response.alternatives && response.alternatives.length > 0) {
      console.log(`   📋 Alternatives: ${response.alternatives.length} other sources available`);
    }

    // Small delay between queries
    await new Promise(resolve => setTimeout(resolve, 500));
  }

  // Print final statistics
  console.log(`\n${'='.repeat(80)}`);
  console.log('📈 System Statistics');
  console.log('='.repeat(80));
  const stats = metrics.getStats();
  console.log(`Total Queries: ${stats.totalQueries}`);
  console.log(`Average Confidence: ${stats.avgConfidence}%`);
  console.log(`Average Latency: ${stats.avgLatency}ms`);
  console.log(`Cache Hit Rate: ${stats.cacheHitRate}%`);
  console.log(`Source Usage:`, stats.sourceUsage);
  console.log();
}

// Run demonstration
if (require.main === module) {
  demonstrateSystem().catch(console.error);
}

// Export for use in other modules
export {
  ConfidenceBasedOrchestrator,
  SequentialShortCircuitStrategy,
  ParallelAggregateStrategy,
  ParallelRaceStrategy,
  KnowledgeBaseSource,
  MockLLMSource,
  MockWebSearchSource,
  InMemoryCacheManager,
  MetricsCollector,
  StrategyFactory
};
