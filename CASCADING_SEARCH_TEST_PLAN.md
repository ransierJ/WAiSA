# Cascading Search - Comprehensive Testing Strategy

**Project**: WAiSA (Windows AI System Administrator)
**Component**: Cascading/Waterfall Search System
**Author**: TESTER Agent (Hive Mind)
**Date**: 2025-10-27
**Version**: 1.0

---

## Executive Summary

This document provides a comprehensive testing strategy for the cascading search system that routes queries through multiple information sources (KB → LLM → MS Docs → Web) with confidence-based decision logic.

**Testing Goals**:
1. Validate confidence scoring accuracy for each source
2. Verify cascade decision logic and early stopping
3. Ensure conflict resolution works correctly
4. Validate timeout handling and error scenarios
5. Achieve 95%+ test coverage for critical paths

**Key Metrics**:
- Unit Test Coverage: Target 90%+
- Integration Test Coverage: Target 85%+
- End-to-End Test Scenarios: 50+ scenarios
- Performance: All tests complete in <5 minutes

---

## Table of Contents

1. [Test Architecture](#test-architecture)
2. [Unit Test Specifications](#unit-test-specifications)
3. [Integration Test Specifications](#integration-test-specifications)
4. [Test Scenarios](#test-scenarios)
5. [Mock/Stub Strategy](#mockstub-strategy)
6. [Test Data Requirements](#test-data-requirements)
7. [Performance Testing](#performance-testing)
8. [Test Execution Plan](#test-execution-plan)

---

## 1. Test Architecture

### 1.1 Testing Stack

```
Testing Framework:        xUnit 2.4.2
Assertion Library:        FluentAssertions 8.8.0
Mocking Framework:        Moq 4.20.72
Code Coverage:            coverlet.collector 6.0.0
Test Runner:              Visual Studio Test Platform 17.6.0
```

**Rationale**: These are already configured in `/home/sysadmin/sysadmin_in_a_box/backend/WAiSA.Tests/WAiSA.Tests.csproj`

### 1.2 Test Organization

```
WAiSA.Tests/
├── Unit/
│   ├── Scoring/
│   │   ├── KBConfidenceScorerTests.cs
│   │   ├── LLMConfidenceScorerTests.cs
│   │   ├── MSDocsConfidenceScorerTests.cs
│   │   └── WebSearchConfidenceScorerTests.cs
│   ├── CascadeLogic/
│   │   ├── CascadeDecisionEngineTests.cs
│   │   ├── EarlyStoppingTests.cs
│   │   └── ThresholdLogicTests.cs
│   ├── ConflictResolution/
│   │   ├── ConflictDetectorTests.cs
│   │   └── MSTieBreakingTests.cs
│   └── SourceIntegrations/
│       ├── KBIntegrationTests.cs
│       ├── LLMIntegrationTests.cs
│       ├── MSDocsIntegrationTests.cs
│       └── WebSearchIntegrationTests.cs
├── Integration/
│   ├── EndToEnd/
│   │   ├── CascadeFlowTests.cs
│   │   └── CompleteJourneyTests.cs
│   ├── MultiSource/
│   │   ├── ParallelRetrievalTests.cs
│   │   └── SequentialCascadeTests.cs
│   └── ErrorHandling/
│       ├── TimeoutTests.cs
│       ├── SourceFailureTests.cs
│       └── PartialResultTests.cs
└── Fixtures/
    ├── TestData.cs
    ├── MockFactories.cs
    └── TestHelpers.cs
```

### 1.3 Test Categories

Use xUnit `[Trait]` attributes for organization:

```csharp
[Trait("Category", "Unit")]
[Trait("Category", "Integration")]
[Trait("Category", "E2E")]
[Trait("Component", "ConfidenceScoring")]
[Trait("Component", "CascadeLogic")]
[Trait("Component", "ConflictResolution")]
[Trait("Priority", "Critical")]
[Trait("Priority", "High")]
[Trait("Priority", "Medium")]
```

---

## 2. Unit Test Specifications

### 2.1 KB Confidence Scorer Tests

**File**: `Unit/Scoring/KBConfidenceScorerTests.cs`

#### Test Cases

##### TC-KB-001: Basic Vector Similarity Scoring
```csharp
[Fact]
[Trait("Category", "Unit")]
[Trait("Component", "ConfidenceScoring")]
public void ScoreKBResult_WithHighVectorSimilarity_ReturnsHighConfidence()
{
    // Arrange
    var scorer = new KBConfidenceScorer();
    var result = new KnowledgeSearchResult
    {
        VectorSimilarity = 0.95,
        UsageCount = 10,
        AverageRating = 4.5,
        LastUpdated = DateTime.UtcNow.AddDays(-5)
    };

    // Act
    var confidence = scorer.CalculateConfidence(result);

    // Assert
    confidence.Should().BeGreaterThan(0.80);
    confidence.Should().BeLessThanOrEqualTo(1.0);
}
```

##### TC-KB-002: Multi-Factor Scoring (60/20/15/5)
```csharp
[Theory]
[InlineData(0.90, 100, 5.0, 0, 0.77)] // High vector, high usage, perfect rating, recent
[InlineData(0.70, 50, 4.0, 30, 0.60)] // Medium vector, medium usage, good rating, older
[InlineData(0.50, 10, 3.0, 90, 0.40)] // Low vector, low usage, okay rating, old
public void ScoreKBResult_WithVariousFactors_CalculatesCorrectly(
    double vectorSim, int usage, double rating, int daysOld, double expectedMin)
{
    // Arrange
    var scorer = new KBConfidenceScorer();
    var result = new KnowledgeSearchResult
    {
        VectorSimilarity = vectorSim,
        UsageCount = usage,
        AverageRating = rating,
        LastUpdated = DateTime.UtcNow.AddDays(-daysOld)
    };

    // Act
    var confidence = scorer.CalculateConfidence(result);

    // Assert
    confidence.Should().BeGreaterThanOrEqualTo(expectedMin);
}
```

##### TC-KB-003: Freshness Decay
```csharp
[Theory]
[InlineData(0, 1.00)]  // Today = no decay
[InlineData(30, 0.90)] // 30 days = 10% decay
[InlineData(90, 0.70)] // 90 days = 30% decay
[InlineData(180, 0.50)] // 180 days = 50% decay
public void ScoreKBResult_WithAgingData_AppliesFreshnessDecay(
    int daysOld, double expectedDecayFactor)
{
    // Arrange
    var scorer = new KBConfidenceScorer();
    var resultNew = CreateKBResult(daysOld: 0);
    var resultOld = CreateKBResult(daysOld: daysOld);

    // Act
    var confidenceNew = scorer.CalculateConfidence(resultNew);
    var confidenceOld = scorer.CalculateConfidence(resultOld);

    // Assert
    var actualDecay = confidenceOld / confidenceNew;
    actualDecay.Should().BeApproximately(expectedDecayFactor, 0.05);
}
```

##### TC-KB-004: Edge Cases
```csharp
[Fact]
public void ScoreKBResult_WithNullResult_ReturnsZeroConfidence()
{
    var scorer = new KBConfidenceScorer();
    var confidence = scorer.CalculateConfidence(null);
    confidence.Should().Be(0.0);
}

[Fact]
public void ScoreKBResult_WithNegativeUsageCount_ClampedToZero()
{
    var scorer = new KBConfidenceScorer();
    var result = CreateKBResult(usageCount: -10);
    var confidence = scorer.CalculateConfidence(result);
    confidence.Should().BeGreaterThanOrEqualTo(0.0);
}
```

##### TC-KB-005: Threshold Validation
```csharp
[Theory]
[InlineData(0.85, true)]  // Above threshold
[InlineData(0.75, true)]  // At threshold
[InlineData(0.70, false)] // Below threshold
public void IsConfidenceSufficient_WithKBThreshold_ReturnsCorrectResult(
    double confidence, bool expectedSufficient)
{
    // Arrange
    var scorer = new KBConfidenceScorer();
    var threshold = 0.75; // KB threshold from config

    // Act
    var isSufficient = scorer.IsConfidenceSufficient(confidence, threshold);

    // Assert
    isSufficient.Should().Be(expectedSufficient);
}
```

**Total KB Tests**: 15-20 tests

---

### 2.2 LLM Confidence Scorer Tests

**File**: `Unit/Scoring/LLMConfidenceScorerTests.cs`

#### Test Cases

##### TC-LLM-001: Token Probability Analysis
```csharp
[Theory]
[InlineData(new[] { 0.9, 0.85, 0.88 }, 0.87)] // High confidence
[InlineData(new[] { 0.6, 0.55, 0.58 }, 0.58)] // Medium confidence
[InlineData(new[] { 0.3, 0.25, 0.35 }, 0.30)] // Low confidence
public void CalculateConfidence_FromTokenProbabilities_ReturnsAverageProbability(
    double[] tokenProbs, double expectedAvg)
{
    // Arrange
    var scorer = new LLMConfidenceScorer();
    var logprobs = tokenProbs.Select(p => Math.Log(p)).ToList();

    // Act
    var confidence = scorer.CalculateFromLogProbs(logprobs);

    // Assert
    confidence.Should().BeApproximately(expectedAvg, 0.02);
}
```

##### TC-LLM-002: Calibration Factor Application
```csharp
[Fact]
public void CalculateConfidence_AppliesCalibrationFactor()
{
    // Arrange
    var scorer = new LLMConfidenceScorer(calibrationFactor: 0.65);
    var rawConfidence = 0.80;

    // Act
    var calibratedConfidence = scorer.ApplyCalibration(rawConfidence);

    // Assert
    calibratedConfidence.Should().BeApproximately(0.52, 0.01); // 0.80 * 0.65
}
```

##### TC-LLM-003: Uncertainty Detection - Phrase Matching
```csharp
[Theory]
[InlineData("I don't know the answer", 0.2)]
[InlineData("I'm not sure about this", 0.3)]
[InlineData("This is definitely correct", 0.85)]
[InlineData("The answer is clearly X", 0.90)]
public void DetectUncertainty_FromResponseText_ReturnsAppropriateConfidence(
    string responseText, double expectedConfidence)
{
    // Arrange
    var scorer = new LLMConfidenceScorer();

    // Act
    var confidence = scorer.DetectUncertaintyFromText(responseText);

    // Assert
    confidence.Should().BeApproximately(expectedConfidence, 0.1);
}
```

##### TC-LLM-004: Self-Consistency Check
```csharp
[Fact]
public async Task CheckSelfConsistency_WithConsistentResponses_ReturnsHighConfidence()
{
    // Arrange
    var scorer = new LLMConfidenceScorer();
    var responses = new[]
    {
        "The capital of France is Paris.",
        "Paris is the capital of France.",
        "France's capital city is Paris."
    };

    // Act
    var consistencyScore = await scorer.CalculateSelfConsistency(responses);

    // Assert
    consistencyScore.Should().BeGreaterThan(0.80);
}

[Fact]
public async Task CheckSelfConsistency_WithContradictoryResponses_ReturnsLowConfidence()
{
    // Arrange
    var scorer = new LLMConfidenceScorer();
    var responses = new[]
    {
        "The capital of France is Paris.",
        "The capital of France is Lyon.",
        "The capital of France is Marseille."
    };

    // Act
    var consistencyScore = await scorer.CalculateSelfConsistency(responses);

    // Assert
    consistencyScore.Should().BeLessThan(0.50);
}
```

##### TC-LLM-005: Combined Confidence Calculation
```csharp
[Fact]
public void CalculateConfidence_CombinesMultipleSignals()
{
    // Arrange
    var scorer = new LLMConfidenceScorer();
    var signals = new LLMConfidenceSignals
    {
        TokenProbability = 0.85,
        TextUncertainty = 0.90,
        SelfConsistency = 0.88,
        CalibrationFactor = 0.65
    };

    // Act
    var finalConfidence = scorer.CalculateOverallConfidence(signals);

    // Assert
    // Should weight: 50% token prob, 30% text, 20% consistency, then apply calibration
    finalConfidence.Should().BeInRange(0.50, 0.70);
}
```

**Total LLM Tests**: 20-25 tests

---

### 2.3 Cascade Decision Engine Tests

**File**: `Unit/CascadeLogic/CascadeDecisionEngineTests.cs`

#### Test Cases

##### TC-CASCADE-001: Early Stopping - KB Sufficient
```csharp
[Fact]
public async Task ShouldContinueCascade_WithHighKBConfidence_ReturnsFalse()
{
    // Arrange
    var engine = new CascadeDecisionEngine();
    var kbResult = new SourceResult
    {
        Source = InformationSource.KnowledgeBase,
        Confidence = 0.85,
        Completeness = 0.90,
        Relevance = 0.88
    };

    // Act
    var shouldContinue = engine.ShouldContinueCascade(kbResult);

    // Assert
    shouldContinue.Should().BeFalse("KB confidence exceeds threshold");
}
```

##### TC-CASCADE-002: Cascade to Next Level
```csharp
[Fact]
public async Task ShouldContinueCascade_WithLowKBConfidence_ReturnsTrue()
{
    // Arrange
    var engine = new CascadeDecisionEngine();
    var kbResult = new SourceResult
    {
        Source = InformationSource.KnowledgeBase,
        Confidence = 0.60,
        Completeness = 0.50,
        Relevance = 0.65
    };

    // Act
    var shouldContinue = engine.ShouldContinueCascade(kbResult);

    // Assert
    shouldContinue.Should().BeTrue("KB confidence below threshold");
}
```

##### TC-CASCADE-003: Multi-Criteria Decision
```csharp
[Theory]
[InlineData(0.85, 0.90, 0.88, false)] // All high - stop
[InlineData(0.85, 0.60, 0.88, true)]  // Low completeness - continue
[InlineData(0.60, 0.90, 0.88, true)]  // Low confidence - continue
[InlineData(0.85, 0.90, 0.60, true)]  // Low relevance - continue
public void ShouldContinueCascade_WithVariousMetrics_MakesCorrectDecision(
    double confidence, double completeness, double relevance, bool expectedContinue)
{
    // Arrange
    var engine = new CascadeDecisionEngine();
    var result = new SourceResult
    {
        Confidence = confidence,
        Completeness = completeness,
        Relevance = relevance
    };

    // Act
    var shouldContinue = engine.ShouldContinueCascade(result);

    // Assert
    shouldContinue.Should().Be(expectedContinue);
}
```

##### TC-CASCADE-004: Source Ordering
```csharp
[Fact]
public void GetNextSource_ReturnsCorrectSequence()
{
    // Arrange
    var engine = new CascadeDecisionEngine();

    // Act & Assert
    engine.GetNextSource(null).Should().Be(InformationSource.KnowledgeBase);
    engine.GetNextSource(InformationSource.KnowledgeBase).Should().Be(InformationSource.LLM);
    engine.GetNextSource(InformationSource.LLM).Should().Be(InformationSource.MicrosoftDocs);
    engine.GetNextSource(InformationSource.MicrosoftDocs).Should().Be(InformationSource.WebSearch);
    engine.GetNextSource(InformationSource.WebSearch).Should().BeNull();
}
```

##### TC-CASCADE-005: Threshold Configuration
```csharp
[Fact]
public void LoadThresholds_FromConfiguration_SetsCorrectValues()
{
    // Arrange
    var config = new CascadeConfiguration
    {
        KBThreshold = 0.75,
        LLMThreshold = 0.70,
        DocsThreshold = 0.65,
        WebThreshold = 0.60
    };
    var engine = new CascadeDecisionEngine(config);

    // Act
    var kbThreshold = engine.GetThreshold(InformationSource.KnowledgeBase);

    // Assert
    kbThreshold.Should().Be(0.75);
}
```

**Total Cascade Tests**: 20-25 tests

---

### 2.4 Conflict Resolution Tests

**File**: `Unit/ConflictResolution/ConflictDetectorTests.cs`

#### Test Cases

##### TC-CONFLICT-001: Detect Conflicting Facts
```csharp
[Fact]
public void DetectConflicts_WithContradictoryResults_ReturnsConflicts()
{
    // Arrange
    var detector = new ConflictDetector();
    var results = new Dictionary<InformationSource, SourceResult>
    {
        [InformationSource.KnowledgeBase] = CreateResult("Python 3.11 is latest", 0.75),
        [InformationSource.LLM] = CreateResult("Python 3.10 is latest", 0.70),
        [InformationSource.MicrosoftDocs] = CreateResult("Python 3.11 is latest", 0.85)
    };

    // Act
    var conflicts = detector.DetectConflicts(results);

    // Assert
    conflicts.Should().NotBeEmpty();
    conflicts.First().ConflictingFacts.Should().Contain("Python 3.11");
    conflicts.First().ConflictingFacts.Should().Contain("Python 3.10");
}
```

##### TC-CONFLICT-002: MS Docs Tie-Breaking
```csharp
[Fact]
public void ResolveConflict_WithMSDocsPresent_UsesMSDocsAsAuthority()
{
    // Arrange
    var resolver = new ConflictResolver();
    var conflict = new Conflict
    {
        ConflictingResults = new Dictionary<InformationSource, SourceResult>
        {
            [InformationSource.KnowledgeBase] = CreateResult("Answer A", 0.75),
            [InformationSource.LLM] = CreateResult("Answer B", 0.80),
            [InformationSource.MicrosoftDocs] = CreateResult("Answer A", 0.70)
        }
    };

    // Act
    var resolution = resolver.ResolveWithAuthority(conflict, InformationSource.MicrosoftDocs);

    // Assert
    resolution.AuthoritativeSource.Should().Be(InformationSource.MicrosoftDocs);
    resolution.SelectedAnswer.Should().Contain("Answer A");
}
```

##### TC-CONFLICT-003: No Conflict Detected
```csharp
[Fact]
public void DetectConflicts_WithConsistentResults_ReturnsEmpty()
{
    // Arrange
    var detector = new ConflictDetector();
    var results = new Dictionary<InformationSource, SourceResult>
    {
        [InformationSource.KnowledgeBase] = CreateResult("Paris is capital of France", 0.85),
        [InformationSource.LLM] = CreateResult("The capital of France is Paris", 0.90)
    };

    // Act
    var conflicts = detector.DetectConflicts(results);

    // Assert
    conflicts.Should().BeEmpty();
}
```

##### TC-CONFLICT-004: Semantic Similarity for Conflict Detection
```csharp
[Theory]
[InlineData("Azure Functions supports Python 3.11", "Python 3.11 is supported by Azure Functions", false)]
[InlineData("Azure Functions supports Python 3.11", "Azure Functions does not support Python 3.11", true)]
public async Task DetectConflicts_UsesSemanticSimilarity_CorrectlyIdentifiesConflicts(
    string text1, string text2, bool shouldConflict)
{
    // Arrange
    var detector = new ConflictDetector(new SemanticAnalyzer());
    var results = new Dictionary<InformationSource, SourceResult>
    {
        [InformationSource.KnowledgeBase] = CreateResult(text1, 0.80),
        [InformationSource.LLM] = CreateResult(text2, 0.85)
    };

    // Act
    var conflicts = await detector.DetectConflictsAsync(results);

    // Assert
    if (shouldConflict)
        conflicts.Should().NotBeEmpty();
    else
        conflicts.Should().BeEmpty();
}
```

**Total Conflict Tests**: 15-20 tests

---

## 3. Integration Test Specifications

### 3.1 End-to-End Cascade Flow Tests

**File**: `Integration/EndToEnd/CascadeFlowTests.cs`

#### Test Cases

##### TC-E2E-001: Complete Cascade Journey
```csharp
[Fact]
[Trait("Category", "Integration")]
[Trait("Priority", "Critical")]
public async Task ExecuteCascade_WithQueryNotInKB_FlowsToWebSearch()
{
    // Arrange
    var orchestrator = CreateOrchestrator();
    var query = "Latest Azure OpenAI GPT-4 Turbo features released today";

    // Act
    var result = await orchestrator.ExecuteCascadeAsync(query);

    // Assert
    result.FinalSource.Should().Be(InformationSource.WebSearch);
    result.CascadeSteps.Should().HaveCount(4); // KB, LLM, Docs, Web
    result.CascadeSteps[0].Source.Should().Be(InformationSource.KnowledgeBase);
    result.CascadeSteps[0].Decision.Should().Contain("insufficient");
}
```

##### TC-E2E-002: Early Stop at KB
```csharp
[Fact]
public async Task ExecuteCascade_WithKBMatch_StopsAtKB()
{
    // Arrange
    var orchestrator = CreateOrchestrator();
    var query = "How to restart Windows Update service"; // Should be in KB

    // Act
    var result = await orchestrator.ExecuteCascadeAsync(query);

    // Assert
    result.FinalSource.Should().Be(InformationSource.KnowledgeBase);
    result.CascadeSteps.Should().HaveCount(1);
    result.TotalLatencyMs.Should().BeLessThan(500);
}
```

##### TC-E2E-003: Early Stop at LLM
```csharp
[Fact]
public async Task ExecuteCascade_WithLLMKnowledge_StopsAtLLM()
{
    // Arrange
    var orchestrator = CreateOrchestrator();
    var query = "What is the capital of France?"; // General knowledge

    // Act
    var result = await orchestrator.ExecuteCascadeAsync(query);

    // Assert
    result.FinalSource.Should().Be(InformationSource.LLM);
    result.CascadeSteps.Should().HaveCount(2); // KB (insufficient), LLM (sufficient)
    result.FinalConfidence.Should().BeGreaterThan(0.70);
}
```

##### TC-E2E-004: MS Docs Authority
```csharp
[Fact]
public async Task ExecuteCascade_MicrosoftQuery_PrioritizesMSDocs()
{
    // Arrange
    var orchestrator = CreateOrchestrator();
    var query = "How to configure Azure Managed Identity for Azure Functions";

    // Act
    var result = await orchestrator.ExecuteCascadeAsync(query);

    // Assert
    result.FinalSource.Should().BeOneOf(
        InformationSource.MicrosoftDocs,
        InformationSource.KnowledgeBase); // If KB has MS Docs content
    result.FinalConfidence.Should().BeGreaterThan(0.65);
}
```

**Total E2E Tests**: 15-20 tests

---

### 3.2 Timeout and Error Handling Tests

**File**: `Integration/ErrorHandling/TimeoutTests.cs`

#### Test Cases

##### TC-TIMEOUT-001: KB Timeout
```csharp
[Fact]
public async Task ExecuteCascade_WhenKBTimesOut_ContinuesToLLM()
{
    // Arrange
    var mockKB = new Mock<IKnowledgeBaseService>();
    mockKB.Setup(x => x.SearchAsync(It.IsAny<string>()))
        .ThrowsAsync(new TimeoutException("KB search timeout"));

    var orchestrator = CreateOrchestratorWithMock(mockKB.Object);
    var query = "Test query";

    // Act
    var result = await orchestrator.ExecuteCascadeAsync(query);

    // Assert
    result.Errors.Should().ContainSingle(e => e.Source == InformationSource.KnowledgeBase);
    result.FinalSource.Should().NotBe(InformationSource.KnowledgeBase);
    result.CascadeSteps.Should().Contain(s => s.Source == InformationSource.LLM);
}
```

##### TC-TIMEOUT-002: All Sources Timeout
```csharp
[Fact]
public async Task ExecuteCascade_WhenAllSourcesTimeout_ReturnsBestPartialResult()
{
    // Arrange
    var orchestrator = CreateOrchestratorWithAllTimeouts();
    var query = "Test query";

    // Act
    var result = await orchestrator.ExecuteCascadeAsync(query);

    // Assert
    result.IsSuccess.Should().BeFalse();
    result.Errors.Should().HaveCount(4); // All sources failed
    result.FinalAnswer.Should().Contain("unable to retrieve");
}
```

##### TC-TIMEOUT-003: Partial Results
```csharp
[Fact]
public async Task ExecuteCascade_WithPartialKBTimeout_UsesCachedResults()
{
    // Arrange
    var mockKB = CreatePartialTimeoutMock(); // Returns 2 of 5 results
    var orchestrator = CreateOrchestratorWithMock(mockKB);
    var query = "Test query";

    // Act
    var result = await orchestrator.ExecuteCascadeAsync(query);

    // Assert
    result.PartialResults.Should().BeTrue();
    result.CascadeSteps[0].ResultCount.Should().BeLessThan(5);
    result.CascadeSteps[0].Warning.Should().Contain("partial");
}
```

**Total Timeout Tests**: 10-12 tests

---

## 4. Test Scenarios

### 4.1 Scenario Matrix

| ID | Scenario | Expected Source | Expected Stop Point | Priority |
|----|----------|----------------|---------------------|----------|
| S-001 | KB has perfect match (>0.85) | KB | KB | Critical |
| S-002 | KB has good match (0.75-0.85) | KB | KB | High |
| S-003 | KB insufficient, LLM knows | LLM | LLM | Critical |
| S-004 | KB+LLM insufficient, MS Docs has | MS Docs | MS Docs | Critical |
| S-005 | All insufficient, need Web | Web | Web | High |
| S-006 | Conflicting KB vs LLM, MS Docs tie-breaks | MS Docs | MS Docs | Critical |
| S-007 | Microsoft-specific query | MS Docs or KB | MS Docs | High |
| S-008 | General knowledge query | LLM | LLM | Medium |
| S-009 | Recent event query | Web | Web | High |
| S-010 | Company-specific policy | KB | KB | Critical |
| S-011 | All sources <0.50 confidence | Best available | End of cascade | Medium |
| S-012 | KB timeout | LLM or later | Non-KB | High |
| S-013 | Empty KB results | LLM | LLM | High |
| S-014 | Contradictory facts across 3 sources | MS Docs | MS Docs | Critical |
| S-015 | Query classification determines skip | Varies | Optimized path | Medium |

### 4.2 Detailed Test Scenarios

#### Scenario S-001: KB Perfect Match

```csharp
[Fact]
[Trait("Scenario", "S-001")]
[Trait("Priority", "Critical")]
public async Task Scenario_KBPerfectMatch_StopsImmediately()
{
    // GIVEN: KB has a document with vector similarity 0.95, high usage, recent
    var kbMock = CreateKBMock(vectorSim: 0.95, usage: 150, rating: 4.8, daysOld: 2);
    var orchestrator = CreateOrchestrator(kbMock);

    // WHEN: User asks question that matches this document
    var query = "How do I restart the Windows Update service?";
    var result = await orchestrator.ExecuteCascadeAsync(query);

    // THEN: System stops at KB with high confidence
    result.FinalSource.Should().Be(InformationSource.KnowledgeBase);
    result.FinalConfidence.Should().BeGreaterThan(0.85);
    result.CascadeSteps.Should().HaveCount(1);
    result.TotalLatencyMs.Should().BeLessThan(300);

    // AND: Does not query LLM, MS Docs, or Web
    kbMock.Verify(x => x.SearchAsync(query), Times.Once);
    result.CascadeSteps.Should().NotContain(s => s.Source != InformationSource.KnowledgeBase);
}
```

#### Scenario S-006: Conflict Resolution

```csharp
[Fact]
[Trait("Scenario", "S-006")]
[Trait("Priority", "Critical")]
public async Task Scenario_ConflictingSourcesMSDocsTieBreaker()
{
    // GIVEN: KB says "Python 3.10", LLM says "Python 3.11", MS Docs says "Python 3.11"
    var kbMock = CreateKBMock("Python 3.10 is latest for Azure Functions", confidence: 0.78);
    var llmMock = CreateLLMMock("Python 3.11 is latest for Azure Functions", confidence: 0.82);
    var docsMock = CreateDocsMock("Python 3.11 is now supported in Azure Functions", confidence: 0.75);
    var orchestrator = CreateOrchestrator(kbMock, llmMock, docsMock);

    // WHEN: User asks about Python version
    var query = "What Python version does Azure Functions support?";
    var result = await orchestrator.ExecuteCascadeAsync(query);

    // THEN: System detects conflict
    result.ConflictsDetected.Should().BeTrue();
    result.Conflicts.Should().HaveCount(1);

    // AND: MS Docs is used as tie-breaker
    result.ConflictResolution.AuthoritativeSource.Should().Be(InformationSource.MicrosoftDocs);
    result.FinalAnswer.Should().Contain("3.11");

    // AND: All conflicting sources are noted
    result.SourcesConsulted.Should().HaveCount(3);
    result.ConflictNote.Should().Contain("Multiple sources provided different information");
}
```

#### Scenario S-012: KB Timeout Recovery

```csharp
[Fact]
[Trait("Scenario", "S-012")]
[Trait("Priority", "High")]
public async Task Scenario_KBTimeout_RecoversByLLM()
{
    // GIVEN: KB service is experiencing timeouts
    var kbMock = new Mock<IKnowledgeBaseService>();
    kbMock.Setup(x => x.SearchAsync(It.IsAny<string>()))
        .ThrowsAsync(new TimeoutException("KB unavailable"));

    var llmMock = CreateLLMMock("Valid LLM response", confidence: 0.85);
    var orchestrator = CreateOrchestrator(kbMock.Object, llmMock.Object);

    // WHEN: User asks a general question
    var query = "What is Docker?";
    var result = await orchestrator.ExecuteCascadeAsync(query);

    // THEN: System gracefully handles KB failure
    result.IsSuccess.Should().BeTrue();
    result.Errors.Should().ContainSingle(e => e.Source == InformationSource.KnowledgeBase);

    // AND: Falls back to LLM
    result.FinalSource.Should().Be(InformationSource.LLM);
    result.FinalConfidence.Should().BeGreaterThan(0.70);

    // AND: User is informed about KB unavailability
    result.Warnings.Should().Contain(w => w.Contains("Knowledge Base unavailable"));
}
```

---

## 5. Mock/Stub Strategy

### 5.1 Mock Architecture

```csharp
/// <summary>
/// Factory for creating test mocks with consistent behavior
/// </summary>
public class MockFactory
{
    public Mock<IKnowledgeBaseService> CreateKBMock(
        double vectorSim = 0.80,
        int usageCount = 50,
        double rating = 4.0,
        int daysOld = 30)
    {
        var mock = new Mock<IKnowledgeBaseService>();
        mock.Setup(x => x.SearchAsync(It.IsAny<string>()))
            .ReturnsAsync(new KnowledgeSearchResult
            {
                VectorSimilarity = vectorSim,
                UsageCount = usageCount,
                AverageRating = rating,
                LastUpdated = DateTime.UtcNow.AddDays(-daysOld),
                Content = "Mock KB content"
            });
        return mock;
    }

    public Mock<IAIService> CreateLLMMock(
        string response = "Mock LLM response",
        double confidence = 0.75,
        List<double> tokenProbs = null)
    {
        var mock = new Mock<IAIService>();
        tokenProbs ??= new List<double> { 0.8, 0.75, 0.78 };

        mock.Setup(x => x.GenerateResponseAsync(It.IsAny<string>()))
            .ReturnsAsync(new LLMResponse
            {
                Text = response,
                TokenProbabilities = tokenProbs,
                FinishReason = "stop"
            });
        return mock;
    }

    public Mock<IMicrosoftDocsService> CreateDocsMock(
        string content = "Mock MS Docs content",
        double score = 0.70)
    {
        var mock = new Mock<IMicrosoftDocsService>();
        mock.Setup(x => x.SearchAsync(It.IsAny<string>()))
            .ReturnsAsync(new DocsSearchResult
            {
                Content = content,
                Title = "Mock Documentation",
                Url = "https://learn.microsoft.com/mock",
                Score = score
            });
        return mock;
    }

    public Mock<IWebSearchService> CreateWebMock(
        string content = "Mock web content",
        double relevance = 0.65)
    {
        var mock = new Mock<IWebSearchService>();
        mock.Setup(x => x.SearchAsync(It.IsAny<string>()))
            .ReturnsAsync(new WebSearchResult
            {
                Content = content,
                Title = "Mock Web Result",
                Url = "https://example.com/mock",
                Relevance = relevance
            });
        return mock;
    }
}
```

### 5.2 Mock Verification Patterns

```csharp
// Verify KB was queried exactly once
kbMock.Verify(x => x.SearchAsync(query), Times.Once);

// Verify LLM was NOT called (early stop)
llmMock.Verify(x => x.GenerateResponseAsync(It.IsAny<string>()), Times.Never);

// Verify MS Docs was called with optimized query
docsMock.Verify(
    x => x.SearchAsync(It.Is<string>(q => q.Contains("Azure"))),
    Times.Once);

// Verify Web search was used as fallback
webMock.Verify(x => x.SearchAsync(It.IsAny<string>()), Times.Once);
```

### 5.3 Test Data Builders

```csharp
public class KnowledgeSearchResultBuilder
{
    private double _vectorSim = 0.80;
    private int _usage = 50;
    private double _rating = 4.0;
    private int _daysOld = 30;

    public KnowledgeSearchResultBuilder WithHighConfidence()
    {
        _vectorSim = 0.95;
        _usage = 150;
        _rating = 4.8;
        _daysOld = 2;
        return this;
    }

    public KnowledgeSearchResultBuilder WithLowConfidence()
    {
        _vectorSim = 0.55;
        _usage = 5;
        _rating = 3.2;
        _daysOld = 120;
        return this;
    }

    public KnowledgeSearchResult Build()
    {
        return new KnowledgeSearchResult
        {
            VectorSimilarity = _vectorSim,
            UsageCount = _usage,
            AverageRating = _rating,
            LastUpdated = DateTime.UtcNow.AddDays(-_daysOld)
        };
    }
}
```

---

## 6. Test Data Requirements

### 6.1 Knowledge Base Test Data

**Required KB Entries** (minimum 50 documents):

```csharp
public static class TestKnowledgeBase
{
    public static List<KBDocument> GetTestDocuments()
    {
        return new List<KBDocument>
        {
            // High-confidence documents
            new KBDocument
            {
                Id = "kb-001",
                Content = "To restart Windows Update service: Stop-Service wuauserv; Start-Service wuauserv",
                Category = "Windows Administration",
                UsageCount = 250,
                AverageRating = 4.9,
                LastUpdated = DateTime.UtcNow.AddDays(-5)
            },

            // Medium-confidence documents
            new KBDocument
            {
                Id = "kb-015",
                Content = "Azure Functions pricing is based on execution time and memory...",
                Category = "Azure",
                UsageCount = 45,
                AverageRating = 4.1,
                LastUpdated = DateTime.UtcNow.AddDays(-60)
            },

            // Low-confidence documents (old, low usage)
            new KBDocument
            {
                Id = "kb-042",
                Content = "Legacy information about Windows Server 2012...",
                Category = "Deprecated",
                UsageCount = 3,
                AverageRating = 3.0,
                LastUpdated = DateTime.UtcNow.AddDays(-365)
            }
        };
    }
}
```

### 6.2 Test Queries by Category

```csharp
public static class TestQueries
{
    // KB should handle (stop at KB)
    public static readonly string[] KBQueries = new[]
    {
        "How do I restart Windows Update service?",
        "What is the command to check disk space in PowerShell?",
        "How to enable Remote Desktop on Windows Server?",
        "What are the best practices for Azure VM management?"
    };

    // LLM should handle (stop at LLM)
    public static readonly string[] LLMQueries = new[]
    {
        "What is the capital of France?",
        "Explain what Docker is in simple terms",
        "What does REST API stand for?",
        "How does DNS work?"
    };

    // MS Docs should handle (continue to MS Docs)
    public static readonly string[] MSDocsQueries = new[]
    {
        "How to configure Azure Managed Identity for Function Apps?",
        "What are the latest features in .NET 8?",
        "How to set up Azure Monitor alerts?",
        "What is the Azure Well-Architected Framework?"
    };

    // Web should handle (full cascade)
    public static readonly string[] WebQueries = new[]
    {
        "What happened at Microsoft Build 2025?",
        "Latest Azure OpenAI model releases today",
        "Current Azure service outages",
        "Breaking news about Windows 12 announcement"
    };

    // Conflict scenarios
    public static readonly string[] ConflictQueries = new[]
    {
        "What Python version does Azure Functions support?",
        "Is Windows Server 2025 released yet?",
        "What is the current LTS version of Node.js?"
    };
}
```

### 6.3 Mock Response Templates

```csharp
public static class MockResponses
{
    public static string KBHighConfidence =>
        "According to our internal knowledge base: {{answer}}. " +
        "This information is verified and up-to-date as of {{date}}.";

    public static string LLMGeneralKnowledge =>
        "Based on my training: {{answer}}. " +
        "This is general knowledge that was accurate as of my training cutoff.";

    public static string MSDocsOfficial =>
        "According to Microsoft documentation: {{answer}}. " +
        "Source: {{url}}. Last updated: {{date}}.";

    public static string WebSearchRecent =>
        "Based on recent web sources: {{answer}}. " +
        "Sources consulted: {{sources}}. Published: {{date}}.";
}
```

---

## 7. Performance Testing

### 7.1 Performance Test Cases

```csharp
[Trait("Category", "Performance")]
public class PerformanceTests
{
    [Fact]
    public async Task CascadeExecution_KB_CompletesUnder300ms()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        var query = TestQueries.KBQueries[0];
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await orchestrator.ExecuteCascadeAsync(query);
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(300);
        result.TotalLatencyMs.Should().BeLessThan(300);
    }

    [Fact]
    public async Task CascadeExecution_FullCascade_CompletesUnder5000ms()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        var query = TestQueries.WebQueries[0];
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await orchestrator.ExecuteCascadeAsync(query);
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000);
        result.CascadeSteps.Should().HaveCount(4);
    }

    [Fact]
    public async Task CascadeExecution_Under_Load_MaintainsLatency()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        var queries = Enumerable.Repeat(TestQueries.KBQueries[0], 100);

        // Act
        var tasks = queries.Select(q => orchestrator.ExecuteCascadeAsync(q));
        var results = await Task.WhenAll(tasks);

        // Assert
        var avgLatency = results.Average(r => r.TotalLatencyMs);
        avgLatency.Should().BeLessThan(500);

        var p95Latency = results.OrderBy(r => r.TotalLatencyMs)
            .Skip(95).First().TotalLatencyMs;
        p95Latency.Should().BeLessThan(1000);
    }
}
```

### 7.2 Performance Benchmarks

| Operation | Target Latency (P50) | Target Latency (P95) | Target Latency (P99) |
|-----------|---------------------|---------------------|---------------------|
| KB Search Only | <200ms | <300ms | <500ms |
| KB + LLM | <2000ms | <2500ms | <3000ms |
| KB + LLM + MS Docs | <3000ms | <4000ms | <5000ms |
| Full Cascade (KB + LLM + Docs + Web) | <4000ms | <5000ms | <6000ms |
| Conflict Resolution | <500ms | <800ms | <1000ms |

---

## 8. Test Execution Plan

### 8.1 Test Execution Order

**Phase 1: Unit Tests** (Run first, fail fast)
```bash
dotnet test --filter "Category=Unit" --logger "console;verbosity=detailed"
```

**Phase 2: Integration Tests** (Run after unit tests pass)
```bash
dotnet test --filter "Category=Integration" --logger "console;verbosity=detailed"
```

**Phase 3: E2E Tests** (Run after integration tests pass)
```bash
dotnet test --filter "Category=E2E" --logger "console;verbosity=detailed"
```

**Phase 4: Performance Tests** (Run separately)
```bash
dotnet test --filter "Category=Performance" --logger "console;verbosity=detailed"
```

### 8.2 CI/CD Integration

```yaml
# azure-pipelines.yml
trigger:
  branches:
    include:
      - main
      - develop
      - feature/*

stages:
  - stage: Test
    jobs:
      - job: UnitTests
        steps:
          - task: DotNetCoreCLI@2
            inputs:
              command: 'test'
              projects: '**/*Tests.csproj'
              arguments: '--filter "Category=Unit" --collect:"XPlat Code Coverage"'
            displayName: 'Run Unit Tests'

      - job: IntegrationTests
        dependsOn: UnitTests
        steps:
          - task: DotNetCoreCLI@2
            inputs:
              command: 'test'
              projects: '**/*Tests.csproj'
              arguments: '--filter "Category=Integration"'
            displayName: 'Run Integration Tests'

      - job: E2ETests
        dependsOn: IntegrationTests
        steps:
          - task: DotNetCoreCLI@2
            inputs:
              command: 'test'
              projects: '**/*Tests.csproj'
              arguments: '--filter "Category=E2E"'
            displayName: 'Run E2E Tests'

      - job: CodeCoverage
        dependsOn: E2ETests
        steps:
          - task: PublishCodeCoverageResults@1
            inputs:
              codeCoverageTool: 'Cobertura'
              summaryFileLocation: '$(Agent.TempDirectory)/**/*coverage.cobertura.xml'
            displayName: 'Publish Code Coverage'
```

### 8.3 Test Coverage Requirements

**Minimum Coverage Targets**:
- Overall: 85%
- Critical Paths (Cascade Logic): 95%
- Confidence Scoring: 90%
- Conflict Resolution: 90%
- Error Handling: 80%

**Generate Coverage Report**:
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
reportgenerator -reports:coverage.cobertura.xml -targetdir:coverage-report
```

### 8.4 Test Maintenance

**Weekly**:
- Review failing tests
- Update test data as KB changes
- Add tests for new scenarios

**Monthly**:
- Review coverage reports
- Refactor slow tests
- Update mock data

**Quarterly**:
- Full test suite audit
- Performance baseline updates
- Test strategy review

---

## 9. Test Validation Checklist

Before merging to main:

- [ ] All unit tests pass (100%)
- [ ] All integration tests pass (100%)
- [ ] Code coverage >85% overall
- [ ] Critical path coverage >95%
- [ ] Performance benchmarks met
- [ ] No test warnings or skipped tests
- [ ] Mock validations correct
- [ ] Test data realistic
- [ ] Documentation updated
- [ ] CI/CD pipeline green

---

## 10. Risk Assessment

### 10.1 Testing Risks

| Risk | Impact | Probability | Mitigation |
|------|--------|------------|------------|
| Flaky tests due to timing | High | Medium | Use deterministic mocks, avoid Thread.Sleep |
| Mock drift from real APIs | High | High | Regular validation against live APIs |
| Insufficient test coverage | High | Medium | Enforce coverage gates in CI/CD |
| Slow test execution | Medium | Medium | Parallelize tests, optimize fixtures |
| Test data staleness | Medium | High | Automated test data refresh |

### 10.2 Quality Gates

**Pre-Merge Requirements**:
1. All tests pass
2. Coverage >85%
3. No high-severity static analysis warnings
4. Performance benchmarks met
5. Code review approved

**Production Deployment Requirements**:
1. All tests pass in prod-like environment
2. Load testing completed
3. Smoke tests pass
4. Rollback plan tested

---

## 11. Testability Review with Coder Agent

### 11.1 Design Review Points

**Review Questions for Coder Agent**:

1. **Dependency Injection**: Are all services injected via interfaces?
   - ✅ Allows easy mocking
   - ✅ Enables test isolation

2. **Async/Await Patterns**: Are all I/O operations async?
   - ✅ Testable with async test methods
   - ✅ No blocking calls

3. **Configuration**: Is threshold configuration externalized?
   - ✅ Can override for tests
   - ✅ No hardcoded values

4. **Logging**: Are all decisions logged?
   - ✅ Verifiable in tests
   - ✅ Helps debug failures

5. **Error Handling**: Are exceptions caught and wrapped?
   - ✅ Testable error scenarios
   - ✅ No silent failures

### 11.2 Recommended Design Changes

**Suggestions to improve testability**:

```csharp
// BEFORE: Hard to test
public class CascadeEngine
{
    private const double KB_THRESHOLD = 0.75; // Hardcoded

    public async Task<Result> Execute(string query)
    {
        var kbResult = await new KBService().Search(query); // Direct instantiation
        if (kbResult.Confidence > KB_THRESHOLD)
            return kbResult;
    }
}

// AFTER: Easy to test
public class CascadeEngine
{
    private readonly IKBService _kbService;
    private readonly CascadeOptions _options;

    public CascadeEngine(IKBService kbService, IOptions<CascadeOptions> options)
    {
        _kbService = kbService;
        _options = options.Value;
    }

    public async Task<Result> Execute(string query)
    {
        var kbResult = await _kbService.SearchAsync(query);
        if (kbResult.Confidence > _options.KBThreshold)
            return kbResult;
    }
}
```

---

## 12. Summary

### 12.1 Test Suite Summary

**Total Tests Planned**: 150-200 tests

**Breakdown**:
- Unit Tests: 80-100 tests (50-60%)
- Integration Tests: 40-50 tests (25-30%)
- E2E Tests: 20-30 tests (12-15%)
- Performance Tests: 10-15 tests (5-8%)

**Estimated Execution Time**:
- Unit Tests: 30-60 seconds
- Integration Tests: 2-3 minutes
- E2E Tests: 3-5 minutes
- Performance Tests: 5-10 minutes
- **Total**: 10-20 minutes for full suite

### 12.2 Success Criteria

✅ **Test Plan Complete**: Comprehensive scenarios defined
✅ **Mock Strategy Clear**: Consistent mock factories available
✅ **Data Requirements Set**: Test data fixtures ready
✅ **Performance Targets**: Clear latency benchmarks
✅ **CI/CD Integration**: Automated pipeline defined

### 12.3 Next Steps

1. **Coordinate with CODER Agent**: Review design for testability
2. **Create Test Project Structure**: Set up test directories
3. **Implement Mock Factories**: Build reusable test fixtures
4. **Write Critical Path Tests First**: Focus on cascade logic
5. **Iterate Based on Findings**: Refine tests as implementation evolves

---

## Appendix A: Test Template

```csharp
using Xunit;
using FluentAssertions;
using Moq;
using WAiSA.Core.Services.Cascade;

namespace WAiSA.Tests.Unit.Cascade
{
    [Trait("Category", "Unit")]
    [Trait("Component", "CascadeLogic")]
    public class ExampleCascadeTests
    {
        private readonly Mock<IKBService> _kbMock;
        private readonly Mock<IAIService> _aiMock;
        private readonly CascadeEngine _sut; // System Under Test

        public ExampleCascadeTests()
        {
            _kbMock = new Mock<IKBService>();
            _aiMock = new Mock<IAIService>();
            _sut = new CascadeEngine(_kbMock.Object, _aiMock.Object);
        }

        [Fact]
        public async Task Execute_WithHighKBConfidence_StopsAtKB()
        {
            // Arrange
            var query = "test query";
            _kbMock.Setup(x => x.SearchAsync(query))
                .ReturnsAsync(CreateHighConfidenceKBResult());

            // Act
            var result = await _sut.ExecuteAsync(query);

            // Assert
            result.FinalSource.Should().Be(InformationSource.KnowledgeBase);
            _kbMock.Verify(x => x.SearchAsync(query), Times.Once);
            _aiMock.Verify(x => x.GenerateAsync(It.IsAny<string>()), Times.Never);
        }

        private KBResult CreateHighConfidenceKBResult()
        {
            return new KBResult
            {
                Confidence = 0.90,
                Completeness = 0.95,
                Relevance = 0.92
            };
        }
    }
}
```

---

**Document Status**: ✅ Complete
**Review Status**: Awaiting Coder Agent Feedback
**Last Updated**: 2025-10-27
**Next Review**: After Coder Agent design validation
