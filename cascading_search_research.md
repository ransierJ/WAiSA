# Cascading/Waterfall Search System - Comprehensive Research Report

## Executive Summary

This document provides comprehensive research and actionable guidance for implementing a cascading/waterfall search system with the following source hierarchy:

1. Knowledge Base (KB) - Primary source
2. LLM's parametric knowledge - Secondary
3. Microsoft Docs - Tertiary
4. Web Search - Fallback
5. Microsoft Docs - Tie-breaker for conflicts

---

## 1. CASCADING SEARCH PATTERNS

### 1.1 Overview

**Cascading/Waterfall Search** is a retrieval pattern where the system progressively queries multiple data sources in a defined order, moving to the next source only when the current source provides insufficient information.

### 1.2 Academic Framework: Pistis-RAG

The **Pistis-RAG** framework (2024) represents state-of-the-art cascading retrieval with five stages:

```
Matching → Pre-Ranking → Ranking → Reasoning → Aggregating
```

**Each Stage Function:**
- **Matching**: Retrieval algorithms select pertinent documents from repositories
- **Pre-Ranking**: Semantic analysis refines document scores based on query relevance
- **Ranking**: Aligns document ranking with LLM preferences for coherent responses
- **Reasoning**: Enhances response diversity, exploring multiple sequences (supports Chain-of-Thought)
- **Aggregation**: Synthesizes responses for coherence

**Key Insight**: Pistis-RAG showed 6.06% increase in MMLU (English) and 7.08% increase in C-EVAL (Chinese) accuracy over baseline RAG systems.

**GitHub**: https://github.com/HuskyInSalt/CRAG

### 1.3 When to Move to Next Source (Decision Triggers)

#### Three-Criteria Framework (UAR - Unified Active Retrieval):

1. **Intent Awareness**: Does the user explicitly want retrieval?
2. **Knowledge Awareness**: Does the question require factual knowledge?
3. **Time-Sensitive Awareness**: Is the answer likely to change over time?

#### Practical Threshold Methods:

**Method 1: Confidence Score Thresholds**
```python
# Recommended starting points
CONFIDENCE_THRESHOLD_KB = 0.7        # High confidence required from KB
CONFIDENCE_THRESHOLD_LLM = 0.6       # Medium confidence from LLM
CONFIDENCE_THRESHOLD_DOCS = 0.5      # Lower threshold for external sources
CONFIDENCE_THRESHOLD_WEB = 0.5       # Same as docs

# Trade-off: Higher threshold = better accuracy, lower coverage
```

**Best Practice**: Start at 0.5 (50/100) threshold and adjust based on accuracy vs coverage requirements.

**Method 2: Query Classification**
- Assign complexity levels: "A" (parametric memory sufficient), "B" (single-step RAG), "C" (multi-step iterative RAG)
- Use lightweight classifier model to route queries

**Method 3: Completeness Detection**
```python
# Key metrics for "sufficient answer" detection:
{
    "answer_relevancy": 0.0-1.0,      # How relevant to query
    "faithfulness": 0.0-1.0,           # No hallucinations
    "context_sufficiency": 0.0-1.0,    # Enough info provided
    "completeness": 0.0-1.0            # All query parts answered
}

# Stop cascade when ALL metrics exceed thresholds
STOP_THRESHOLD = {
    "answer_relevancy": 0.75,
    "faithfulness": 0.85,
    "context_sufficiency": 0.70,
    "completeness": 0.80
}
```

### 1.4 Cost Optimization Strategies

**1. Caching Layer**
- Store common and static information in cache
- Reduces latency and costs significantly
- Redis or in-memory cache for hot queries

**2. Model Selection Tiers**
```python
# Cost-optimized model routing
MODEL_TIERS = {
    "simple_query": "claude-3-haiku",       # Cheap, fast
    "medium_query": "claude-3-5-sonnet",    # Balanced
    "complex_query": "claude-3-opus"        # Expensive, best quality
}
```

**3. Asynchronous Processing**
- Implement async retrieval for non-blocking operations
- Batch processing for offline/high-volume scenarios

**4. Early Stopping**
- Implement aggressive early stopping when confidence thresholds met
- Avoid unnecessary downstream queries

**5. Cascade Short-Circuiting**
```python
# Stop immediately on high-confidence answers
if kb_confidence > 0.9 and completeness > 0.9:
    return kb_result  # Skip LLM, Docs, Web entirely
```

### 1.5 Latency Management

**Latency Budget Framework**:
```python
LATENCY_BUDGETS = {
    "kb_search": 200,          # ms
    "llm_generation": 2000,    # ms
    "ms_docs_search": 500,     # ms
    "web_search": 1000,        # ms
    "total_max": 5000          # ms (hard limit)
}
```

**Parallel Retrieval Pattern**:
```python
# For non-dependent sources, query in parallel
import asyncio

async def parallel_search(query):
    kb_task = asyncio.create_task(search_kb(query))
    docs_task = asyncio.create_task(search_ms_docs(query))

    # Get results as they complete
    done, pending = await asyncio.wait(
        [kb_task, docs_task],
        return_when=asyncio.FIRST_COMPLETED
    )

    # Use first high-confidence result
    for task in done:
        result = await task
        if result.confidence > 0.8:
            # Cancel remaining tasks
            for p in pending:
                p.cancel()
            return result
```

---

## 2. KNOWLEDGE BASE INTEGRATION

### 2.1 Popular KB Solutions

#### Vector Databases Comparison:

| Database | Strengths | Use Case | Latency |
|----------|-----------|----------|---------|
| **Pinecone** | Fully managed, serverless, low-latency | Production RAG, real-time | Single-digit ms for 10-NN |
| **Weaviate** | Hybrid search (vector + keyword), advanced filtering | Complex queries | Single-digit ms for millions of items |
| **ChromaDB** | Simple API, easy local development | Development, prototyping | Fast, local-first |
| **Azure AI Search** | Enterprise features, semantic ranking, integrated | Microsoft ecosystem | Optimized with HNSW |

#### Graph Databases:

| Database | Strengths | Use Case |
|----------|-----------|----------|
| **Neo4j** | Relationship traversal, GraphRAG | Knowledge graphs, connected data |
| **Azure Cosmos DB** | Multi-model, global distribution | Enterprise graph + document |

### 2.2 Effective KB Querying

**Hybrid Search Pattern** (Recommended):
```python
from azure.search.documents import SearchClient
from azure.search.documents.models import VectorizedQuery

def hybrid_kb_search(query, vector_embedding, top_k=5):
    """
    Combines keyword + vector + semantic ranking
    Best precision/recall results
    """
    vector_query = VectorizedQuery(
        vector=vector_embedding,
        k_nearest_neighbors=top_k,
        fields="content_vector"
    )

    results = search_client.search(
        search_text=query,              # Keyword search
        vector_queries=[vector_query],   # Vector search
        query_type="semantic",           # Semantic ranking
        semantic_configuration_name="my-config",
        select=["id", "content", "metadata"]
    )

    return results
```

**Use Cases by Search Type**:
- **Semantic Search**: When concepts differ from exact words (e.g., "turn phone on" vs "toggling power")
- **Keyword Search**: Specific technical terms, model numbers (e.g., "HD7-8D")
- **Hybrid Search**: Combines both (e.g., "how do I turn on my HD7-8D?")

### 2.3 Relevance Scoring for KB Results

**Azure AI Search Scores**:
```python
# Score ranges and interpretation
SCORE_RANGES = {
    "@search.score": (0.0, float('inf')),      # BM25 score (higher is better)
    "@search.reranker_score": (0.0, 4.0),      # Semantic score (higher is better)
    "similarity_score": (0.0, 1.0)             # Cosine similarity
}

# Threshold recommendations
QUALITY_THRESHOLDS = {
    "excellent": {
        "reranker_score": 3.0,
        "similarity": 0.85
    },
    "good": {
        "reranker_score": 2.5,
        "similarity": 0.75
    },
    "acceptable": {
        "reranker_score": 2.0,
        "similarity": 0.65
    }
}
```

**Filtering Results**:
```python
def filter_kb_results(results, min_reranker_score=2.5):
    """
    Only return chunks exceeding threshold
    Higher threshold = fewer but more relevant chunks
    """
    filtered = [
        r for r in results
        if r.get("@search.reranker_score", 0) >= min_reranker_score
    ]
    return filtered
```

### 2.4 When KB Result is "Good Enough"

**Multi-Metric Decision**:
```python
def is_kb_sufficient(kb_results, query):
    """
    Determine if KB results are sufficient to stop cascade
    """
    if not kb_results:
        return False, "No results"

    top_result = kb_results[0]

    # Check score threshold
    reranker_score = top_result.get("@search.reranker_score", 0)
    if reranker_score < 2.5:
        return False, "Low relevance score"

    # Check completeness (using LLM judge)
    completeness = evaluate_completeness(query, top_result["content"])
    if completeness < 0.75:
        return False, "Incomplete answer"

    # Check result count (multiple high-quality results = good coverage)
    high_quality_count = sum(
        1 for r in kb_results
        if r.get("@search.reranker_score", 0) >= 2.5
    )
    if high_quality_count < 2:
        return False, "Insufficient coverage"

    return True, "KB sufficient"
```

### 2.5 GraphRAG for Enhanced KB

**When to Use Graph KB**:
- Complex multi-hop queries spanning multiple entities
- Relationship-heavy domains (org charts, system architectures)
- Need for explainable reasoning paths

**Implementation with Neo4j + LangChain**:
```python
from langchain.graphs import Neo4jGraph
from langchain.chains import GraphCypherQAChain

# Initialize graph
graph = Neo4jGraph(
    url="bolt://localhost:7687",
    username="neo4j",
    password="password"
)

# Query with natural language
qa_chain = GraphCypherQAChain.from_llm(
    llm=llm,
    graph=graph,
    verbose=True
)

# Combines vector search with graph traversal
response = qa_chain.run("How are System A and System B connected?")
```

**Hybrid Vector + Graph Pattern**:
```python
# 1. Initial vector search for entry points
initial_results = vector_db.similarity_search(query, k=5)

# 2. Graph traversal for related entities
for result in initial_results:
    entity_id = result.metadata["entity_id"]
    # Traverse graph to find connected context
    related = graph.query(f"""
        MATCH (e:Entity {{id: '{entity_id}'}})-[r*1..2]-(related)
        RETURN related, r
    """)

# 3. Combine results
combined_context = merge_results(initial_results, related)
```

---

## 3. LLM KNOWLEDGE VS RETRIEVED KNOWLEDGE

### 3.1 Distinguishing Between Knowledge Sources

**Parametric Knowledge (PK)**: Knowledge captured during pre-training, stored in model weights.

**Contextual/Retrieved Knowledge (CK)**: External information provided at inference time.

**Four Relationship Types**:
1. **Supportive**: Retrieved info confirms parametric knowledge
2. **Complementary**: Retrieved info adds to parametric knowledge
3. **Conflicting**: Retrieved info contradicts parametric knowledge
4. **Irrelevant**: Retrieved info unrelated to parametric knowledge

### 3.2 When to Trust LLM vs Force Retrieval

**Decision Matrix**:

| Query Type | Trust LLM? | Force Retrieval? | Reason |
|------------|------------|------------------|--------|
| General knowledge (e.g., "What is Python?") | Yes | No | Well-known, unlikely to change |
| Recent events (e.g., "Latest Azure features") | No | Yes | Post-training data |
| Specific facts (e.g., "Company policy X") | No | Yes | Not in training data |
| Simple calculations | Yes | No | Reliable parametric capability |
| Domain expertise | Depends | Yes | Verify critical information |

**Implementation Pattern**:
```python
def should_retrieve(query, llm_response):
    """
    Determine if retrieval is needed based on query classification
    """
    # Classify query intent
    intent = classify_query(query)

    # Intent-based routing
    if intent in ["factual_recent", "proprietary", "specific_product"]:
        return True, "Requires external knowledge"

    # Check LLM uncertainty
    uncertainty = detect_uncertainty(llm_response)
    if uncertainty > 0.5:
        return True, "LLM uncertain"

    # Time-sensitive check
    if is_time_sensitive(query):
        return True, "May be outdated"

    return False, "LLM sufficient"
```

### 3.3 Detecting "I Don't Know" in LLM Responses

**Problem**: LLMs often make up facts instead of admitting uncertainty.

**Detection Methods**:

#### Method 1: Self-Consistency Check (SelfCheckGPT)
```python
async def check_self_consistency(query, num_samples=5):
    """
    Generate multiple responses and check for contradictions
    """
    responses = await asyncio.gather(*[
        llm.generate(query) for _ in range(num_samples)
    ])

    # Check if responses contradict or entail each other
    consistency_score = compute_consistency(responses)

    # Low consistency = likely hallucination
    if consistency_score < 0.5:
        return "uncertain", "Contradictory responses"

    return "confident", "Consistent responses"
```

#### Method 2: Token Probability Analysis
```python
def detect_uncertainty_from_logprobs(response, logprobs):
    """
    Low token probabilities indicate uncertainty
    """
    avg_prob = sum(logprobs) / len(logprobs)

    if avg_prob < 0.3:
        return "high_uncertainty"
    elif avg_prob < 0.6:
        return "medium_uncertainty"
    else:
        return "confident"
```

#### Method 3: Explicit Uncertainty Phrases
```python
UNCERTAINTY_PATTERNS = [
    r"i don't know",
    r"i'm not sure",
    r"i cannot say",
    r"unclear",
    r"uncertain",
    r"may be",
    r"might be",
    r"possibly",
    r"i don't have.*information"
]

def has_uncertainty_phrase(response):
    import re
    for pattern in UNCERTAINTY_PATTERNS:
        if re.search(pattern, response.lower()):
            return True
    return False
```

#### Method 4: Adversarial Testing
```python
def test_parametric_vs_retrieved(llm, query, fake_context):
    """
    Provide incorrect context to see if LLM follows it or uses parametric
    """
    # Correct answer in parametric memory: "Paris"
    fake_prompt = f"""
    Context: The capital of France is Lyon.
    Question: What is the capital of France?
    """

    response = llm.generate(fake_prompt)

    if "Paris" in response:
        return "using_parametric"  # Ignoring retrieved
    elif "Lyon" in response:
        return "using_retrieved"   # Following context
    else:
        return "uncertain"
```

### 3.4 Forcing LLM to Prioritize Retrieved Knowledge

**Prompt Engineering Pattern**:
```python
GROUNDED_PROMPT = """
You are an assistant that ONLY answers using the provided context.

STRICT RULES:
1. Answer ONLY with facts from the context below
2. If the context doesn't contain the answer, say "I don't have enough information"
3. Do NOT use your general knowledge
4. Do NOT make assumptions

Context:
{context}

Question: {query}

Answer:
"""
```

**System Message Pattern**:
```python
SYSTEM_MESSAGE = """
You are a retrieval-augmented assistant. Your knowledge is explicitly limited to:
1. The provided search results
2. Nothing else

If the search results don't answer the question:
- Say "I don't have information about that"
- Do NOT fill in gaps with general knowledge
- Do NOT make up information
"""
```

---

## 4. MICROSOFT DOCS INTEGRATION

### 4.1 Microsoft Learn MCP Server

**Official Microsoft MCP Server** - Available NOW!

**Connection Details**:
```json
{
  "microsoft.docs.mcp": {
    "type": "http",
    "url": "https://learn.microsoft.com/api/mcp"
  }
}
```

**Key Features**:
- No authentication required
- No charge to use
- Real-time access to latest Microsoft documentation
- Semantic search across all Microsoft Learn content
- Returns JSON-encoded chunks with citations

**Available Tools**:
1. `microsoft_docs_search` - Semantic search across Microsoft Learn
2. `microsoft_docs_fetch` - Fetch complete documentation pages
3. `microsoft_code_sample_search` - Search code samples with language filtering

### 4.2 Using Microsoft Docs MCP in Cascade

**Implementation Example**:
```python
from mcp import Client

# Initialize MCP client
mcp_client = Client("https://learn.microsoft.com/api/mcp")

async def search_microsoft_docs(query, max_results=5):
    """
    Search Microsoft Learn documentation
    """
    results = await mcp_client.call_tool(
        "microsoft_docs_search",
        arguments={"query": query}
    )

    # Results include:
    # - title
    # - contentUrl
    # - content (excerpt, max 500 tokens each)

    return results
```

**Code Sample Search**:
```python
async def search_microsoft_code_samples(query, language="python"):
    """
    Search for specific code examples
    """
    results = await mcp_client.call_tool(
        "microsoft_code_sample_search",
        arguments={
            "query": query,
            "language": language  # python, csharp, javascript, etc.
        }
    )

    return results
```

### 4.3 Best Practices for MS Docs Retrieval

**1. Query Optimization**:
```python
def optimize_query_for_ms_docs(original_query):
    """
    Add Microsoft-specific context to queries
    """
    # Detect if query is about Microsoft products
    ms_products = ["Azure", "Microsoft 365", ".NET", "Visual Studio", etc.]

    if any(product.lower() in original_query.lower() for product in ms_products):
        # Already specific
        return original_query

    # Add context if ambiguous
    return f"Microsoft {original_query}"
```

**2. Result Scoring**:
```python
def score_ms_docs_result(result, query):
    """
    Score MS Docs results for relevance
    """
    score = 0.0

    # Title match
    if query.lower() in result["title"].lower():
        score += 0.3

    # Content relevance (simple keyword matching)
    query_terms = set(query.lower().split())
    content_terms = set(result["content"].lower().split())
    overlap = len(query_terms & content_terms) / len(query_terms)
    score += overlap * 0.5

    # Recency (prefer newer docs)
    # Would need publication date from metadata

    # Authority (official docs > community)
    if "learn.microsoft.com" in result["contentUrl"]:
        score += 0.2

    return score
```

**3. Follow-up Pattern**:
```python
async def comprehensive_ms_docs_search(query):
    """
    Search first, then fetch full pages for top results
    """
    # 1. Initial search
    search_results = await search_microsoft_docs(query, max_results=10)

    # 2. Score and rank
    scored_results = [
        (score_ms_docs_result(r, query), r)
        for r in search_results
    ]
    scored_results.sort(reverse=True)

    # 3. Fetch full content for top 3
    full_results = []
    for score, result in scored_results[:3]:
        full_doc = await mcp_client.call_tool(
            "microsoft_docs_fetch",
            arguments={"url": result["contentUrl"]}
        )
        full_results.append(full_doc)

    return full_results
```

---

## 5. WEB SEARCH INTEGRATION

### 5.1 Web Search APIs Comparison

| API | Strengths | Pricing | Best For |
|-----|-----------|---------|----------|
| **Tavily AI** | LLM-optimized, aggregates 20+ sources, RAG-ready | Paid tiers | AI agents, RAG applications |
| **Bing Search API** | Microsoft ecosystem, high quality | 10x price increase (2023), expensive | Enterprise with Microsoft contracts |
| **WebSearchAPI.ai** | Google-quality, pre-extracted clean content | Paid | AI applications, clean structured data |
| **Serper** | Google Search API wrapper, fast | Pay-per-use | General purpose, cost-effective |
| **SerpAPI** | Multi-engine support | Pay-per-use | Need multiple search engines |

### 5.2 Tavily Integration (Recommended for AI)

**Why Tavily**:
- Specifically designed for LLMs and AI agents
- Aggregates 20+ sources automatically
- Uses proprietary AI to score, filter, and rank content
- Returns concise, LLM-ready insights in single API call
- Includes answers, raw content, and images

**LangChain Integration**:
```python
from langchain_tavily import TavilySearchResults

# Initialize
tavily_search = TavilySearchResults(
    api_key="your-tavily-key",
    max_results=5,
    include_raw_content=True,
    include_images=False
)

# Search
results = tavily_search.invoke("How to implement RAG with Azure AI Search")

# Results are pre-processed and ready for LLM consumption
```

**Direct API Usage**:
```python
import requests

def tavily_search(query, max_results=5):
    """
    Search using Tavily API
    """
    response = requests.post(
        "https://api.tavily.com/search",
        json={
            "api_key": TAVILY_API_KEY,
            "query": query,
            "max_results": max_results,
            "include_answer": True,
            "include_raw_content": True
        }
    )

    data = response.json()

    return {
        "answer": data.get("answer"),  # Direct answer if available
        "results": data.get("results"), # List of sources
        "search_depth": data.get("search_depth")
    }
```

### 5.3 Result Filtering and Ranking

**Multi-Stage Filtering**:
```python
def filter_web_results(results, query):
    """
    Filter and rank web search results
    """
    filtered = []

    for result in results:
        # Stage 1: Quality filters
        if not passes_quality_check(result):
            continue

        # Stage 2: Relevance scoring
        relevance = compute_relevance(result, query)
        if relevance < 0.6:
            continue

        # Stage 3: Deduplication
        if is_duplicate(result, filtered):
            continue

        result["relevance_score"] = relevance
        filtered.append(result)

    # Sort by relevance
    filtered.sort(key=lambda x: x["relevance_score"], reverse=True)

    return filtered


def passes_quality_check(result):
    """
    Basic quality heuristics
    """
    # Minimum content length
    if len(result.get("content", "")) < 100:
        return False

    # Block known low-quality domains
    LOW_QUALITY_DOMAINS = [
        "stackoverflow.com",  # Usually too specific/code-heavy
        "reddit.com",         # Varies in quality
        "quora.com"           # Mixed quality
    ]

    url = result.get("url", "")
    if any(domain in url for domain in LOW_QUALITY_DOMAINS):
        return False

    return True


def compute_relevance(result, query):
    """
    Simple relevance scoring
    """
    score = 0.0

    # Title relevance
    title = result.get("title", "").lower()
    query_lower = query.lower()
    if query_lower in title:
        score += 0.4

    # Content relevance
    content = result.get("content", "").lower()
    query_terms = set(query_lower.split())
    content_terms = set(content.split())
    overlap = len(query_terms & content_terms) / len(query_terms)
    score += overlap * 0.6

    return min(score, 1.0)
```

### 5.4 Integration with AWS Bedrock Agents

**Pattern for Enterprise Integration**:
```python
import boto3

def setup_bedrock_with_web_search():
    """
    Integrate web search with AWS Bedrock Agents
    """
    # Lambda function for web search
    lambda_client = boto3.client('lambda')

    # Bedrock agent configuration
    bedrock_config = {
        "agent_name": "rag-agent",
        "tools": [
            {
                "name": "web_search",
                "description": "Search the web for current information",
                "lambda_function": "web-search-lambda"
            }
        ]
    }

    return bedrock_config
```

---

## 6. TIE-BREAKING LOGIC

### 6.1 Detecting Source Conflicts

**Conflict Detection Pattern**:
```python
def detect_conflicts(sources_results):
    """
    Identify when multiple sources provide conflicting information
    """
    # Extract key facts from each source
    kb_facts = extract_facts(sources_results["kb"])
    llm_facts = extract_facts(sources_results["llm"])
    docs_facts = extract_facts(sources_results["ms_docs"])
    web_facts = extract_facts(sources_results["web"])

    # Compare facts using LLM
    conflicts = []
    all_facts = kb_facts + llm_facts + docs_facts + web_facts

    for i, fact1 in enumerate(all_facts):
        for fact2 in all_facts[i+1:]:
            if are_conflicting(fact1, fact2):
                conflicts.append({
                    "fact1": fact1,
                    "fact2": fact2,
                    "sources": [fact1["source"], fact2["source"]]
                })

    return conflicts


def are_conflicting(fact1, fact2):
    """
    Use LLM to determine if two facts conflict
    """
    prompt = f"""
    Determine if these two statements conflict with each other.

    Statement 1: {fact1['text']}
    Statement 2: {fact2['text']}

    Do they provide contradictory information? Answer: Yes or No
    If Yes, explain the conflict.
    """

    response = llm.generate(prompt)
    return "yes" in response.lower()
```

### 6.2 Using Microsoft Docs as Authoritative Source

**Prioritization Logic**:
```python
def resolve_conflict(conflict, all_sources):
    """
    Use Microsoft Docs as tie-breaker for conflicts
    """
    # Check if MS Docs has information about this
    ms_docs_result = all_sources.get("ms_docs")

    if not ms_docs_result:
        # No MS Docs info, use other criteria
        return fallback_resolution(conflict, all_sources)

    # Check if MS Docs addresses the conflicting facts
    ms_docs_content = ms_docs_result["content"]

    if conflict["fact1"]["text"] in ms_docs_content:
        return {
            "resolution": "use_fact1",
            "reason": "Confirmed by Microsoft Docs (authoritative)",
            "source": "ms_docs"
        }
    elif conflict["fact2"]["text"] in ms_docs_content:
        return {
            "resolution": "use_fact2",
            "reason": "Confirmed by Microsoft Docs (authoritative)",
            "source": "ms_docs"
        }
    else:
        # MS Docs doesn't clearly support either
        # Use semantic similarity to MS Docs content
        similarity1 = compute_similarity(conflict["fact1"], ms_docs_content)
        similarity2 = compute_similarity(conflict["fact2"], ms_docs_content)

        if similarity1 > similarity2:
            return {
                "resolution": "use_fact1",
                "reason": "More aligned with Microsoft Docs",
                "confidence": similarity1
            }
        else:
            return {
                "resolution": "use_fact2",
                "reason": "More aligned with Microsoft Docs",
                "confidence": similarity2
            }
```

### 6.3 Conflict Resolution Algorithms

**Truth Discovery Research Approach**:
```python
def bayesian_truth_discovery(sources_results):
    """
    Use Bayesian analysis to determine truth considering source reliability
    """
    # Source reliability priors (based on domain)
    SOURCE_RELIABILITY = {
        "ms_docs": 0.95,      # Highest - official documentation
        "kb": 0.85,           # High - curated internal knowledge
        "llm": 0.70,          # Medium - depends on training
        "web": 0.60           # Lower - varies by source
    }

    # For each conflicting statement, compute posterior probability
    posteriors = {}

    for source, result in sources_results.items():
        if not result:
            continue

        prior = SOURCE_RELIABILITY[source]
        likelihood = result.get("confidence", 0.5)

        # Simple Bayes: P(true|evidence) ∝ P(evidence|true) * P(true)
        posterior = likelihood * prior
        posteriors[source] = posterior

    # Highest posterior wins
    best_source = max(posteriors.items(), key=lambda x: x[1])

    return {
        "authoritative_source": best_source[0],
        "confidence": best_source[1],
        "all_posteriors": posteriors
    }
```

### 6.4 Presenting Conflicting Information to Users

**Transparency Pattern**:
```python
def format_conflicting_results(conflicts, resolution):
    """
    Present conflicts and resolution to user
    """
    response = {
        "answer": resolution["selected_answer"],
        "confidence": resolution["confidence"],
        "conflicts_detected": len(conflicts),
        "resolution_method": "microsoft_docs_authoritative"
    }

    # Add conflict details if significant
    if conflicts:
        response["note"] = f"""
        Note: Multiple sources provided different information.

        Based on Microsoft official documentation (authoritative source):
        {resolution["selected_answer"]}

        Alternative perspectives from other sources:
        """

        for conflict in conflicts:
            response["note"] += f"\n- {conflict['source']}: {conflict['claim']}"

    return response
```

**Example Output**:
```
Answer: Azure Functions supports Python 3.8, 3.9, 3.10, and 3.11

Confidence: 0.95
Source: Microsoft Docs (Official)

Note: Some sources mentioned different version support:
- Web search result suggested Python 3.7 is still supported (outdated information)
- LLM parametric knowledge mentioned up to Python 3.10 only (training cutoff)

This answer is based on the official Microsoft documentation as of 2024.
```

---

## 7. IMPLEMENTATION FRAMEWORKS

### 7.1 LangChain Router Pattern

**Query Routing with Structured Output**:
```python
from typing import Literal
from langchain_core.prompts import ChatPromptTemplate
from langchain_core.pydantic_v1 import BaseModel, Field
from langchain_openai import ChatOpenAI

class RouteQuery(BaseModel):
    """Route a user query to the most relevant datasource."""
    datasource: Literal["kb", "llm", "ms_docs", "web_search"] = Field(
        ...,
        description="Choose which datasource would be most relevant"
    )
    reasoning: str = Field(
        ...,
        description="Explain why this datasource was chosen"
    )

llm = ChatOpenAI(model="gpt-4", temperature=0)
structured_llm = llm.with_structured_output(RouteQuery)

system_prompt = """You are an expert at routing queries to the right data source.

Guidelines:
- Use 'kb' for: Internal company knowledge, proprietary information
- Use 'llm' for: General knowledge, common facts, simple questions
- Use 'ms_docs' for: Microsoft product questions, Azure, .NET, M365
- Use 'web_search' for: Recent events, news, current information

Question: {question}
"""

prompt = ChatPromptTemplate.from_messages([
    ("system", system_prompt),
    ("human", "{question}"),
])

router = prompt | structured_llm

# Use it
result = router.invoke({"question": "How to use Azure OpenAI?"})
print(f"Route to: {result.datasource}")
print(f"Reasoning: {result.reasoning}")
```

### 7.2 LlamaIndex Router Query Engine

**Multi-Source Routing**:
```python
from llama_index.core.query_engine import RouterQueryEngine
from llama_index.core.selectors import PydanticSingleSelector
from llama_index.core.tools import QueryEngineTool

# Define tools for each source
kb_tool = QueryEngineTool.from_defaults(
    query_engine=kb_query_engine,
    description="Internal knowledge base with company policies and procedures"
)

ms_docs_tool = QueryEngineTool.from_defaults(
    query_engine=ms_docs_query_engine,
    description="Official Microsoft documentation for Azure, .NET, and Microsoft products"
)

web_tool = QueryEngineTool.from_defaults(
    query_engine=web_search_engine,
    description="Web search for current events and recent information"
)

# Create router
query_engine = RouterQueryEngine(
    selector=PydanticSingleSelector.from_defaults(),
    query_engine_tools=[kb_tool, ms_docs_tool, web_tool],
)

# Use it
response = query_engine.query("What are the latest Azure AI features?")
```

**Multi-Selector for Parallel Queries**:
```python
from llama_index.core.selectors import PydanticMultiSelector

# Allow querying multiple sources simultaneously
query_engine = RouterQueryEngine(
    selector=PydanticMultiSelector.from_defaults(),
    query_engine_tools=[kb_tool, ms_docs_tool, web_tool],
    # Aggregates results from multiple sources
)

response = query_engine.query("Compare internal and external Azure best practices")
# Will query both KB and MS Docs, then combine results
```

### 7.3 Adaptive RAG Frameworks

**Self-RAG Pattern**:
```python
def self_rag_generate(query):
    """
    Self-RAG: Model decides when to retrieve during generation
    """
    context = ""
    response = ""

    # Generate with special reflection tokens
    for step in range(max_steps):
        # Generate next part
        next_part, reflection = llm.generate_with_reflection(
            query=query,
            context=context,
            previous=response
        )

        # Check reflection token
        if reflection == "[Retrieve]":
            # Model signals it needs more information
            new_context = retrieve_documents(query + " " + response)
            context += new_context
            continue

        response += next_part

        if reflection == "[IsSupported]":
            # Check if statement is supported by context
            if not is_supported(next_part, context):
                # Hallucination detected, retrieve more
                context += retrieve_documents(next_part)
                continue

        if reflection == "[IsRelevant]":
            # Check if response is relevant to query
            if not is_relevant(response, query):
                break

    return response
```

**CRAG (Corrective RAG) Pattern**:
```python
def corrective_rag(query):
    """
    CRAG: Evaluates and corrects retrieval quality
    """
    # 1. Initial retrieval
    documents = retrieve_documents(query)

    # 2. Evaluate retrieval quality with lightweight evaluator
    quality_score = evaluate_retrieval_quality(documents, query)

    if quality_score > 0.8:
        # High quality - use directly
        return generate_response(query, documents)

    elif quality_score > 0.5:
        # Medium quality - decompose and recompose
        # Filter out irrelevant parts
        filtered_docs = decompose_and_filter(documents, query)
        return generate_response(query, filtered_docs)

    else:
        # Low quality - fallback to web search
        web_results = search_web(query)
        return generate_response(query, web_results)
```

### 7.4 Semantic Kernel Pattern

```python
from semantic_kernel import Kernel
from semantic_kernel.connectors.ai.open_ai import AzureChatCompletion
from semantic_kernel.connectors.memory.azure_cognitive_search import (
    AzureCognitiveSearchMemoryStore
)

# Initialize kernel
kernel = Kernel()

# Add AI service
kernel.add_service(
    AzureChatCompletion(
        deployment_name="gpt-4",
        endpoint=endpoint,
        api_key=api_key
    )
)

# Add memory connector
memory_store = AzureCognitiveSearchMemoryStore(
    vector_size=1536,
    search_endpoint=search_endpoint,
    admin_key=search_key
)

# Define cascade function
@kernel.function(
    name="cascade_search",
    description="Search multiple sources in cascade"
)
async def cascade_search(query: str) -> str:
    # 1. Search KB
    kb_results = await memory_store.search_async(
        collection="kb",
        query=query,
        limit=5
    )

    if is_sufficient(kb_results):
        return format_results(kb_results)

    # 2. Try MS Docs
    docs_results = await search_ms_docs(query)

    if is_sufficient(docs_results):
        return format_results(docs_results)

    # 3. Fallback to web
    web_results = await search_web(query)
    return format_results(web_results)
```

---

## 8. DECISION LOGIC & THRESHOLDS

### 8.1 Automatic "Stop" Decision

**Multi-Criteria Stopping Logic**:
```python
class CascadeController:
    def __init__(self):
        self.thresholds = {
            "confidence": 0.75,
            "completeness": 0.80,
            "relevance": 0.75,
            "result_count": 3  # Minimum high-quality results
        }

    def should_stop_cascade(self, results, query):
        """
        Determine if cascade should stop at current level
        """
        if not results:
            return False, "No results"

        # Evaluate results
        metrics = self.evaluate_results(results, query)

        # Check all criteria
        checks = {
            "confidence": metrics["confidence"] >= self.thresholds["confidence"],
            "completeness": metrics["completeness"] >= self.thresholds["completeness"],
            "relevance": metrics["relevance"] >= self.thresholds["relevance"],
            "coverage": metrics["high_quality_count"] >= self.thresholds["result_count"]
        }

        # All must pass
        if all(checks.values()):
            return True, "All criteria met"

        # Report which failed
        failed = [k for k, v in checks.items() if not v]
        return False, f"Failed criteria: {failed}"

    def evaluate_results(self, results, query):
        """
        Compute metrics for results
        """
        # Confidence: Average score of top results
        confidence = np.mean([
            r.get("@search.reranker_score", 0) / 4.0  # Normalize to 0-1
            for r in results[:3]
        ])

        # Completeness: Using LLM judge
        completeness = self.check_completeness(results, query)

        # Relevance: Semantic similarity
        relevance = self.compute_relevance(results[0], query)

        # High-quality count
        high_quality_count = sum(
            1 for r in results
            if r.get("@search.reranker_score", 0) >= 2.5
        )

        return {
            "confidence": confidence,
            "completeness": completeness,
            "relevance": relevance,
            "high_quality_count": high_quality_count
        }
```

### 8.2 Detecting "Doesn't Have Answer"

**Negative Signal Detection**:
```python
def detect_no_answer(results, query):
    """
    Detect when source explicitly doesn't have the answer
    """
    # Check 1: Empty results
    if not results or len(results) == 0:
        return True, "No results returned"

    # Check 2: Low scores across all results
    max_score = max(r.get("@search.reranker_score", 0) for r in results)
    if max_score < 1.5:  # Below "acceptable" threshold
        return True, "All results below quality threshold"

    # Check 3: Results don't relate to query
    relevance_scores = [
        compute_semantic_similarity(r["content"], query)
        for r in results
    ]
    if max(relevance_scores) < 0.5:
        return True, "Results not relevant to query"

    # Check 4: Content analysis for uncertainty phrases
    top_result = results[0]
    uncertainty_phrases = [
        "no information",
        "not available",
        "unknown",
        "not documented"
    ]
    if any(phrase in top_result["content"].lower() for phrase in uncertainty_phrases):
        return True, "Source indicates no information"

    return False, "Source has potential answer"
```

### 8.3 Confidence Thresholds vs Explicit Signals

**Hybrid Approach** (Recommended):
```python
class DecisionStrategy:
    def __init__(self):
        # Confidence thresholds (statistical)
        self.confidence_thresholds = {
            "high": 0.85,    # Definitely stop
            "medium": 0.70,  # Check explicit signals
            "low": 0.50      # Definitely continue
        }

        # Explicit signals (rule-based)
        self.stop_signals = [
            "complete_answer_generated",
            "all_query_aspects_covered",
            "high_user_satisfaction_predicted"
        ]

        self.continue_signals = [
            "partial_answer_only",
            "missing_key_information",
            "low_result_quality"
        ]

    def make_decision(self, results, query, context):
        """
        Hybrid decision using both thresholds and signals
        """
        confidence = self.compute_confidence(results)
        signals = self.extract_signals(results, query, context)

        # High confidence: Always stop
        if confidence >= self.confidence_thresholds["high"]:
            return "STOP", "High confidence"

        # Low confidence: Always continue
        if confidence < self.confidence_thresholds["low"]:
            return "CONTINUE", "Low confidence"

        # Medium confidence: Check explicit signals
        if any(signal in signals for signal in self.stop_signals):
            return "STOP", f"Signal: {signals}"

        if any(signal in signals for signal in self.continue_signals):
            return "CONTINUE", f"Signal: {signals}"

        # Default: err on side of caution, continue
        return "CONTINUE", "Medium confidence, no clear signal"
```

### 8.4 Threshold Tuning Process

**Empirical Tuning Approach**:
```python
def tune_thresholds(validation_queries, ground_truth):
    """
    Tune thresholds on validation set
    """
    # Grid search over threshold values
    confidence_range = np.arange(0.5, 0.95, 0.05)
    completeness_range = np.arange(0.6, 0.95, 0.05)

    best_f1 = 0
    best_thresholds = None

    for conf_threshold in confidence_range:
        for comp_threshold in completeness_range:
            # Test with these thresholds
            predictions = []

            for query, truth in zip(validation_queries, ground_truth):
                result = cascade_search(
                    query,
                    confidence_threshold=conf_threshold,
                    completeness_threshold=comp_threshold
                )
                predictions.append(result)

            # Compute metrics
            precision, recall, f1 = compute_metrics(predictions, ground_truth)

            if f1 > best_f1:
                best_f1 = f1
                best_thresholds = {
                    "confidence": conf_threshold,
                    "completeness": comp_threshold
                }

    return best_thresholds
```

---

## 9. REAL-WORLD EXAMPLES & BEST PRACTICES

### 9.1 Production Case Studies

#### DoorDash - Delivery Support Chatbot

**Architecture**:
```
User Query → Query Condensation → RAG Search (KB + Past Cases) → LLM Generation → Guardrail Check → Response
```

**Key Components**:
1. Conversation condensation for context
2. KB search for relevant articles
3. Historical case search
4. LLM generation with retrieved context
5. LLM guardrail for safety
6. LLM judge for quality

**Lessons**:
- Context compression critical for long conversations
- Historical cases improve resolution speed
- Guardrails prevent hallucinations in production

#### Royal Bank of Canada - Arcane System

**Use Case**: Policy retrieval across internal platforms

**Architecture**:
```
Query → Semantic Search (Vector DB) → Re-ranking → Policy Retrieval → Citation Generation
```

**Impact**: Specialists locate relevant policies quickly, boosting productivity

**Lessons**:
- Re-ranking significantly improves relevance
- Citation/provenance critical for compliance
- Fast retrieval (< 1s) essential for user adoption

#### IBM Watson Health

**Use Case**: Cancer diagnosis and treatment recommendations

**Architecture**:
```
Patient Data → EHR Analysis → Medical Literature Search → Knowledge Graph → Treatment Recommendation → Expert Validation
```

**Accuracy**: 96% match with expert oncologists

**Lessons**:
- Domain-specific knowledge graphs crucial
- Human expert validation loop necessary
- Multi-source grounding improves accuracy

### 9.2 Common Pitfalls & Solutions

#### Pitfall 1: Query Pattern Mismatch

**Problem**: Production queries differ significantly from development/test queries

**Solution**:
```python
# Monitor query patterns
query_analyzer = QueryAnalyzer()
query_analyzer.log_query(query, results, user_feedback)

# Regularly analyze
patterns = query_analyzer.analyze_patterns(timeframe="7d")

# Adapt retrieval strategy
if patterns.show_drift():
    update_retrieval_strategy(patterns)
```

#### Pitfall 2: Latency Explosion

**Problem**: Cascade adds latency, especially with sequential queries

**Solution**:
```python
# Parallel retrieval where possible
async def parallel_cascade(query):
    # Start all sources simultaneously
    kb_task = asyncio.create_task(search_kb(query))
    docs_task = asyncio.create_task(search_ms_docs(query))

    # Use first good result
    done, pending = await asyncio.wait(
        [kb_task, docs_task],
        return_when=asyncio.FIRST_COMPLETED
    )

    for task in done:
        result = await task
        if result.confidence > 0.8:
            # Cancel others
            for p in pending:
                p.cancel()
            return result
```

#### Pitfall 3: Context Window Overflow

**Problem**: Too many retrieval results exceed LLM context window

**Solution**:
```python
def manage_context_budget(all_results, max_tokens=8000):
    """
    Prioritize and truncate results to fit context window
    """
    # Reserve tokens for query and response
    RESERVE_TOKENS = 2000
    available_tokens = max_tokens - RESERVE_TOKENS

    # Sort by relevance
    sorted_results = sorted(
        all_results,
        key=lambda x: x["relevance_score"],
        reverse=True
    )

    # Add results until budget exhausted
    selected = []
    token_count = 0

    for result in sorted_results:
        result_tokens = count_tokens(result["content"])
        if token_count + result_tokens <= available_tokens:
            selected.append(result)
            token_count += result_tokens
        else:
            break

    return selected
```

#### Pitfall 4: Stale KB Content

**Problem**: Knowledge base becomes outdated, provides incorrect information

**Solution**:
```python
# Add freshness metadata
class KBDocument:
    def __init__(self, content, created_at, updated_at):
        self.content = content
        self.created_at = created_at
        self.updated_at = updated_at
        self.freshness_score = self.compute_freshness()

    def compute_freshness(self):
        age_days = (datetime.now() - self.updated_at).days
        # Decay function: newer = higher score
        return 1.0 / (1.0 + age_days / 30)  # 30-day half-life

# Consider freshness in ranking
def rank_with_freshness(results):
    for r in results:
        r["final_score"] = (
            r["relevance_score"] * 0.7 +
            r["freshness_score"] * 0.3
        )

    return sorted(results, key=lambda x: x["final_score"], reverse=True)
```

### 9.3 Best Practices Summary

#### DO:
1. **Start with high-quality KB** - Invest in curation and maintenance
2. **Use hybrid search** - Combine vector + keyword + semantic ranking
3. **Implement caching** - Dramatically reduces latency and cost
4. **Add observability** - Log queries, results, decisions for analysis
5. **Tune thresholds empirically** - Use validation set, not intuition
6. **Provide citations** - Always show sources for transparency
7. **Handle conflicts explicitly** - Don't silently ignore contradictions
8. **Optimize for latency** - Users expect <2s responses

#### DON'T:
1. **Don't always retrieve** - Sometimes LLM knowledge is sufficient
2. **Don't ignore confidence scores** - Use them for decision making
3. **Don't cascade blindly** - Stop early when you have good answer
4. **Don't overwhelm context window** - Prioritize and truncate
5. **Don't skip evaluation** - Measure precision, recall, F1
6. **Don't forget edge cases** - Handle empty results, timeouts, errors
7. **Don't deploy without monitoring** - Track performance in production
8. **Don't use old data** - Mark stale content, prefer fresh sources

---

## 10. COMPLETE IMPLEMENTATION EXAMPLE

### 10.1 End-to-End Cascade System

```python
import asyncio
from typing import Dict, List, Optional, Tuple
from dataclasses import dataclass
from enum import Enum

class SourceType(Enum):
    KB = "knowledge_base"
    LLM = "llm_parametric"
    MS_DOCS = "microsoft_docs"
    WEB = "web_search"

@dataclass
class SearchResult:
    source: SourceType
    content: str
    confidence: float
    relevance: float
    completeness: float
    metadata: Dict

class CascadingSearchSystem:
    def __init__(self):
        # Thresholds (tuned on validation set)
        self.thresholds = {
            "kb": {
                "confidence": 0.75,
                "completeness": 0.80,
                "relevance": 0.75
            },
            "llm": {
                "confidence": 0.70,
                "completeness": 0.75,
                "relevance": 0.70
            },
            "ms_docs": {
                "confidence": 0.65,
                "completeness": 0.75,
                "relevance": 0.70
            },
            "web": {
                "confidence": 0.60,
                "completeness": 0.70,
                "relevance": 0.65
            }
        }

        # Initialize connectors
        self.kb_client = KnowledgeBaseClient()
        self.llm_client = LLMClient()
        self.ms_docs_client = MicrosoftDocsClient()
        self.web_search_client = WebSearchClient()

        # Conflict resolver
        self.conflict_resolver = ConflictResolver()

        # Monitoring
        self.metrics = MetricsCollector()

    async def search(self, query: str) -> SearchResult:
        """
        Main cascade search method
        """
        start_time = time.time()

        # 1. Query classification
        query_type = self.classify_query(query)
        self.metrics.log_query(query, query_type)

        # 2. Execute cascade
        try:
            result = await self._execute_cascade(query, query_type)
        except Exception as e:
            self.metrics.log_error(query, str(e))
            raise

        # 3. Log metrics
        latency = time.time() - start_time
        self.metrics.log_result(query, result, latency)

        return result

    async def _execute_cascade(
        self,
        query: str,
        query_type: str
    ) -> SearchResult:
        """
        Execute cascading search across sources
        """
        all_results = {}

        # Level 1: Knowledge Base
        kb_result = await self._search_kb(query)
        all_results[SourceType.KB] = kb_result

        if self._is_sufficient(kb_result, "kb"):
            return self._format_response(kb_result, all_results)

        # Level 2: LLM Parametric Knowledge
        # Check if query is suitable for LLM
        if self._should_use_llm(query, query_type):
            llm_result = await self._query_llm(query, kb_result)
            all_results[SourceType.LLM] = llm_result

            if self._is_sufficient(llm_result, "llm"):
                return self._format_response(llm_result, all_results)

        # Level 3: Microsoft Docs
        # Check if query is about Microsoft products
        if self._is_microsoft_related(query):
            docs_result = await self._search_ms_docs(query)
            all_results[SourceType.MS_DOCS] = docs_result

            if self._is_sufficient(docs_result, "ms_docs"):
                return self._format_response(docs_result, all_results)

        # Level 4: Web Search (Fallback)
        web_result = await self._search_web(query)
        all_results[SourceType.WEB] = web_result

        # Level 5: Conflict Resolution
        # Use MS Docs as tie-breaker if conflicts detected
        final_result = await self._resolve_and_synthesize(
            query,
            all_results
        )

        return final_result

    async def _search_kb(self, query: str) -> Optional[SearchResult]:
        """
        Search internal knowledge base
        """
        try:
            # Hybrid search: vector + keyword + semantic
            results = await self.kb_client.hybrid_search(
                query=query,
                top_k=5,
                semantic_ranking=True
            )

            if not results:
                return None

            # Evaluate results
            metrics = self._evaluate_results(results, query)

            return SearchResult(
                source=SourceType.KB,
                content=self._merge_results(results),
                confidence=metrics["confidence"],
                relevance=metrics["relevance"],
                completeness=metrics["completeness"],
                metadata={"results": results, "count": len(results)}
            )
        except Exception as e:
            print(f"KB search failed: {e}")
            return None

    async def _query_llm(
        self,
        query: str,
        kb_result: Optional[SearchResult]
    ) -> Optional[SearchResult]:
        """
        Query LLM with optional KB context
        """
        try:
            # Construct prompt
            if kb_result:
                # RAG pattern: LLM + KB context
                prompt = f"""
                Context from knowledge base:
                {kb_result.content}

                Question: {query}

                Instructions:
                - Use the context if relevant
                - If context is insufficient, use your knowledge
                - Be explicit about your confidence
                """
            else:
                # Pure LLM response
                prompt = f"""
                Question: {query}

                Instructions:
                - Answer based on your knowledge
                - If uncertain, say so explicitly
                - Provide confidence level
                """

            response = await self.llm_client.generate(
                prompt=prompt,
                temperature=0.3,
                include_logprobs=True
            )

            # Detect uncertainty
            uncertainty = self._detect_uncertainty(response)

            if uncertainty > 0.5:
                return None  # Too uncertain, continue cascade

            # Evaluate response
            metrics = self._evaluate_llm_response(response, query)

            return SearchResult(
                source=SourceType.LLM,
                content=response.text,
                confidence=1.0 - uncertainty,
                relevance=metrics["relevance"],
                completeness=metrics["completeness"],
                metadata={
                    "uncertainty": uncertainty,
                    "avg_logprob": response.avg_logprob
                }
            )
        except Exception as e:
            print(f"LLM query failed: {e}")
            return None

    async def _search_ms_docs(self, query: str) -> Optional[SearchResult]:
        """
        Search Microsoft Learn documentation
        """
        try:
            # Use MCP server
            results = await self.ms_docs_client.search(
                query=query,
                max_results=5
            )

            if not results:
                return None

            # Score and filter results
            scored_results = [
                (self._score_ms_docs_result(r, query), r)
                for r in results
            ]
            scored_results.sort(reverse=True)

            # Take top results
            top_results = [r for score, r in scored_results[:3]]

            # Evaluate
            metrics = self._evaluate_results(top_results, query)

            return SearchResult(
                source=SourceType.MS_DOCS,
                content=self._merge_results(top_results),
                confidence=metrics["confidence"],
                relevance=metrics["relevance"],
                completeness=metrics["completeness"],
                metadata={
                    "results": top_results,
                    "urls": [r["contentUrl"] for r in top_results]
                }
            )
        except Exception as e:
            print(f"MS Docs search failed: {e}")
            return None

    async def _search_web(self, query: str) -> Optional[SearchResult]:
        """
        Web search as fallback
        """
        try:
            # Use Tavily for LLM-optimized results
            results = await self.web_search_client.search(
                query=query,
                max_results=5,
                include_answer=True
            )

            if not results:
                return None

            # Filter and rank
            filtered = self._filter_web_results(results, query)

            if not filtered:
                return None

            # Evaluate
            metrics = self._evaluate_results(filtered, query)

            return SearchResult(
                source=SourceType.WEB,
                content=self._merge_results(filtered),
                confidence=metrics["confidence"],
                relevance=metrics["relevance"],
                completeness=metrics["completeness"],
                metadata={
                    "results": filtered,
                    "urls": [r["url"] for r in filtered]
                }
            )
        except Exception as e:
            print(f"Web search failed: {e}")
            return None

    def _is_sufficient(
        self,
        result: Optional[SearchResult],
        source: str
    ) -> bool:
        """
        Determine if result is sufficient to stop cascade
        """
        if not result:
            return False

        thresholds = self.thresholds[source]

        # All criteria must be met
        sufficient = (
            result.confidence >= thresholds["confidence"] and
            result.completeness >= thresholds["completeness"] and
            result.relevance >= thresholds["relevance"]
        )

        return sufficient

    async def _resolve_and_synthesize(
        self,
        query: str,
        all_results: Dict[SourceType, Optional[SearchResult]]
    ) -> SearchResult:
        """
        Resolve conflicts and synthesize final answer
        """
        # Remove None results
        valid_results = {
            k: v for k, v in all_results.items() if v is not None
        }

        if not valid_results:
            raise ValueError("No valid results from any source")

        # Detect conflicts
        conflicts = self.conflict_resolver.detect_conflicts(valid_results)

        if conflicts:
            # Use MS Docs as tie-breaker
            resolution = self.conflict_resolver.resolve_with_authority(
                conflicts,
                valid_results,
                authority_source=SourceType.MS_DOCS
            )
        else:
            # No conflicts, use highest confidence result
            resolution = max(
                valid_results.items(),
                key=lambda x: x[1].confidence
            )[1]

        # Synthesize final response
        final_content = await self._synthesize_response(
            query,
            resolution,
            valid_results,
            conflicts
        )

        return SearchResult(
            source=resolution.source,
            content=final_content,
            confidence=resolution.confidence,
            relevance=resolution.relevance,
            completeness=resolution.completeness,
            metadata={
                "all_sources": list(valid_results.keys()),
                "conflicts": conflicts,
                "resolution_method": "microsoft_docs_authority"
            }
        )

    async def _synthesize_response(
        self,
        query: str,
        primary_result: SearchResult,
        all_results: Dict[SourceType, SearchResult],
        conflicts: List
    ) -> str:
        """
        Create final synthesized response with citations
        """
        synthesis_prompt = f"""
        Synthesize a comprehensive answer to: {query}

        Primary source ({primary_result.source.value}):
        {primary_result.content}

        Additional sources:
        {self._format_additional_sources(all_results, primary_result.source)}

        {'Conflicts detected: ' + str(conflicts) if conflicts else ''}

        Instructions:
        - Provide clear, accurate answer
        - Cite sources inline
        - Note conflicts if any
        - Acknowledge limitations
        """

        response = await self.llm_client.generate(
            prompt=synthesis_prompt,
            temperature=0.2
        )

        return response.text


# Usage Example
async def main():
    system = CascadingSearchSystem()

    query = "How do I configure Azure OpenAI with managed identity authentication?"

    result = await system.search(query)

    print(f"Answer: {result.content}")
    print(f"Source: {result.source.value}")
    print(f"Confidence: {result.confidence:.2f}")
    print(f"Sources used: {result.metadata['all_sources']}")

if __name__ == "__main__":
    asyncio.run(main())
```

---

## 11. CONCLUSION & RECOMMENDATIONS

### 11.1 Key Takeaways

1. **Cascading search is essential** for production RAG systems handling diverse queries
2. **Thresholds must be empirically tuned** on representative validation data
3. **Hybrid search (vector + keyword + semantic)** provides best retrieval quality
4. **Microsoft Docs MCP server** provides free, real-time access to official documentation
5. **Conflict resolution** with authoritative sources (MS Docs) builds trust
6. **Latency management** through caching and parallel retrieval is critical
7. **Observability** enables continuous improvement in production

### 11.2 Recommended Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Query Router/Classifier                  │
│           (Determines query type and routing strategy)       │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
          ┌──────────────────────┐
          │   Level 1: KB Search │
          │   (Hybrid: Vector +  │
          │    Keyword + Semantic)│
          └──────────┬───────────┘
                     │ Insufficient
                     ▼
          ┌──────────────────────┐
          │  Level 2: LLM Check  │
          │  (Parametric Knowledge│
          │   with Uncertainty    │
          │      Detection)       │
          └──────────┬───────────┘
                     │ Insufficient
                     ▼
          ┌──────────────────────┐
          │Level 3: MS Docs (MCP)│
          │  (Official Microsoft  │
          │   Documentation)      │
          └──────────┬───────────┘
                     │ Insufficient
                     ▼
          ┌──────────────────────┐
          │ Level 4: Web Search  │
          │    (Tavily AI for    │
          │    LLM-optimized)    │
          └──────────┬───────────┘
                     │
                     ▼
          ┌──────────────────────┐
          │ Conflict Resolution  │
          │  (MS Docs Authority) │
          └──────────┬───────────┘
                     │
                     ▼
          ┌──────────────────────┐
          │  Response Synthesis  │
          │   (With Citations)   │
          └──────────────────────┘
```

### 11.3 Next Steps

1. **Implement core cascade logic** with 2-3 sources initially
2. **Deploy with extensive logging** to understand query patterns
3. **Build evaluation pipeline** with precision/recall metrics
4. **Tune thresholds** on production-like validation set
5. **Add caching layer** for frequently accessed content
6. **Implement monitoring** for latency, cost, quality
7. **Iterate based on user feedback** and metrics

### 11.4 Additional Resources

**Code Repositories**:
- Pistis-RAG: https://github.com/HuskyInSalt/CRAG
- Microsoft Learn MCP: https://github.com/MicrosoftDocs/mcp
- Azure Search OpenAI Demo: https://github.com/Azure-Samples/azure-search-openai-demo
- RAG Experiment Accelerator: https://github.com/microsoft/rag-experiment-accelerator

**Documentation**:
- Microsoft Learn MCP: https://learn.microsoft.com/en-us/training/support/mcp
- Azure AI Search RAG: https://learn.microsoft.com/en-us/azure/search/retrieval-augmented-generation-overview
- LangChain Routing: https://python.langchain.com/docs/how_to/routing/
- LlamaIndex Router: https://docs.llamaindex.ai/en/stable/module_guides/querying/router/

**Papers**:
- Pistis-RAG: https://arxiv.org/abs/2407.00072
- Self-RAG: Search for "Self-RAG: Learning to Retrieve, Generate, and Critique"
- CRAG: https://arxiv.org/abs/2401.15884
- Adaptive RAG: https://blog.reachsumit.com/posts/2025/10/learning-to-retrieve/

---

**Document Version**: 1.0
**Last Updated**: 2025-10-27
**Research Completed By**: Claude (Anthropic)
