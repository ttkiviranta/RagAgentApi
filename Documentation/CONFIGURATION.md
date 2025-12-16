# RAG Agent API - Configuration Guide

## ?? Overview

This document provides detailed information about configuring the RAG Agent API.

---

## ?? RAG Modes

### Mode: `hybrid` (Default - Recommended)

**When to use:**
- Interactive chatbot applications
- General Q&A systems
- Educational assistants
- Customer support bots

**Behavior:**
- **With documents (>50% relevance)**: Answers based on ingested documents
  - Response prefix: "?? Vastaus dokumenttien perusteella:"
  - Sources included with relevance scores
  
- **Without documents**: Falls back to ChatGPT general knowledge
  - Response prefix: "?? Dokumenteista ei löytynyt tietoa. Vastaan yleisen tietämykseni perusteella:"
  - No sources (since not from documents)

**Configuration:**
```json
{
  "RagSettings": {
    "Mode": "hybrid"
  }
}
```

**Example responses:**

*With documents:*
```
?? Vastaus dokumenttien perusteella:

PostgreSQL on tehokas avoimen lähdekoodin relaatiotietokanta...

Sources:
- https://postgresql.org/docs (95% match)
- https://wiki.postgresql.org/... (87% match)
```

*Without documents:*
```
?? Dokumenteista ei löytynyt tietoa. Vastaan yleisen tietämykseni perusteella:

PostgreSQL on laajasti käytetty avoimen lähdekoodin relaatiotietokantajärjestelmä, 
joka on tunnettu luotettavuudestaan ja monipuolisista ominaisuuksistaan...
```

---

### Mode: `strict`

**When to use:**
- Legal or compliance-critical systems
- Medical information systems
- Technical documentation systems
- Any application where accuracy > helpfulness

**Behavior:**
- **With documents (>50% relevance)**: Answers based on ingested documents
- **Without documents**: Returns error message

**Configuration:**
```json
{
  "RagSettings": {
    "Mode": "strict"
  }
}
```

**Example responses:**

*With documents:*
```
?? Vastaus dokumenttien perusteella:

PostgreSQL on tehokas avoimen lähdekoodin relaatiotietokanta...

Sources:
- https://postgresql.org/docs (95% match)
```

*Without documents:*
```
Kontekstissa ei ole tietoa tähän kysymykseen. 
Varmista että olet ensin ladannut dokumentteja järjestelmään 
käyttämällä 'Ingest Document' -toimintoa.
```

---

## ?? Complete RagSettings

```json
{
  "RagSettings": {
    "Mode": "hybrid",                   // "hybrid" or "strict"
    "DefaultChunkSize": 1000,           // Default text chunk size
    "DefaultChunkOverlap": 200,         // Overlap between chunks
    "MinChunkSize": 100,                // Minimum allowed chunk size
    "MaxChunkSize": 5000,               // Maximum allowed chunk size
    "DefaultTopK": 5,                   // Default number of results
    "MaxTopK": 50,                      // Maximum allowed results
    "BatchSize": 100,                   // Embedding batch size
    "VectorDimensions": 1536,           // OpenAI ada-002 dimensions
    "MinimumSearchScore": 0.5           // Minimum relevance (0-1)
  }
}
```

### Setting Descriptions

#### Mode
- **Type**: `string`
- **Values**: `"hybrid"` or `"strict"`
- **Default**: `"hybrid"`
- **Description**: Controls behavior when no documents are found

#### DefaultChunkSize
- **Type**: `integer`
- **Range**: 100-5000
- **Default**: 1000
- **Description**: Default size for text chunks when splitting documents

#### DefaultChunkOverlap
- **Type**: `integer`
- **Range**: 0 to ChunkSize/2
- **Default**: 200
- **Description**: Number of characters to overlap between adjacent chunks

#### MinimumSearchScore
- **Type**: `double`
- **Range**: 0.0-1.0
- **Default**: 0.5
- **Description**: Minimum cosine similarity score (50% match) for including results

---

## ?? Customizing Responses

### Changing Prefixes

To customize the response prefixes, edit `Hubs/ChatHub.cs`:

```csharp
// For document-based answers
var prefix = "?? Vastaus dokumenttien perusteella:\n\n";

// For general knowledge fallback
var disclaimerPrefix = "?? Dokumenteista ei löytynyt tietoa. Vastaan yleisen tietämykseni perusteella:\n\n";

// For strict mode error
fullAnswer = "Kontekstissa ei ole tietoa tähän kysymykseen. " +
            "Varmista että olet ensin ladannut dokumentteja järjestelmään käyttämällä 'Ingest Document' -toimintoa.";
```

### Language Localization

Create language-specific prefix files:

```csharp
// In ChatHub.cs
var language = _configuration.GetValue<string>("RagSettings:Language", "fi");

var prefixes = language switch
{
    "en" => ("?? Answer based on documents:\n\n", 
             "?? No documents found. Answering from general knowledge:\n\n"),
    "fi" => ("?? Vastaus dokumenttien perusteella:\n\n", 
             "?? Dokumenteista ei löytynyt tietoa. Vastaan yleisen tietämykseni perusteella:\n\n"),
    _ => ("?? Answer:\n\n", "?? No context:\n\n")
};
```

---

## ?? Security Considerations

### Hybrid Mode Risks
- **Information mixing**: Users might confuse document facts with general knowledge
- **Hallucination risk**: ChatGPT can generate plausible but incorrect information
- **Compliance issues**: May not meet regulatory requirements

**Mitigation:**
- Clear visual indicators (emoji prefixes)
- Log all general knowledge responses for audit
- Consider strict mode for sensitive applications

### Strict Mode Benefits
- **Guaranteed accuracy**: Only answers from verified documents
- **Audit trail**: All responses traceable to source documents
- **Compliance friendly**: Meets regulatory requirements

**Trade-offs:**
- **User frustration**: "I don't know" responses
- **Requires more documents**: Larger knowledge base needed
- **Less conversational**: More formal interaction

---

## ?? Monitoring & Analytics

### Tracking Mode Usage

```sql
-- Count responses by mode
SELECT 
    CASE 
        WHEN sources IS NULL THEN 'general_knowledge'
        ELSE 'document_based'
    END as response_mode,
    COUNT(*) as count
FROM messages
WHERE role = 'assistant'
AND created_at > NOW() - INTERVAL '7 days'
GROUP BY response_mode;
```

### Response Quality Metrics

```sql
-- Average response length by mode
SELECT 
    CASE 
        WHEN sources IS NULL THEN 'general_knowledge'
        ELSE 'document_based'
    END as response_mode,
    AVG(LENGTH(content)) as avg_length,
    COUNT(*) as count
FROM messages
WHERE role = 'assistant'
GROUP BY response_mode;
```

---

## ?? Best Practices

### Hybrid Mode
? **Do:**
- Use clear visual indicators for response types
- Log all general knowledge responses
- Regularly review general knowledge usage
- Educate users about the dual-mode behavior

? **Don't:**
- Use for regulated industries without approval
- Mix document and general knowledge in same response
- Remove source attribution
- Disable logging

### Strict Mode
? **Do:**
- Provide helpful error messages
- Guide users to document ingestion
- Monitor "no context" frequency
- Maintain comprehensive document coverage

? **Don't:**
- Silently fail without explanation
- Use generic error messages
- Ignore "no context" patterns
- Over-rely on minimum relevance threshold

---

## ?? Migration Guide

### From Strict to Hybrid

1. **Update configuration:**
```json
{
  "RagSettings": {
    "Mode": "hybrid"
  }
}
```

2. **Test with sample queries:**
   - Verify document-based responses work
   - Test general knowledge fallback
   - Check prefix visibility

3. **Monitor initial usage:**
   - Track general knowledge usage rate
   - Review response quality
   - Gather user feedback

### From Hybrid to Strict

1. **Analyze current usage:**
```sql
-- See how many responses use general knowledge
SELECT COUNT(*) FROM messages 
WHERE role = 'assistant' AND sources IS NULL;
```

2. **Identify knowledge gaps:**
   - Review common queries without context
   - Plan document ingestion strategy

3. **Update configuration:**
```json
{
  "RagSettings": {
    "Mode": "strict"
  }
}
```

4. **Communicate changes:**
   - Update user documentation
   - Add help messages about document requirements

---

## ?? Support

For questions about configuration:
- Check this guide first
- Review `README.md` for general setup
- See `API_Endpoints.md` for API details
- Create GitHub issue for configuration problems

---

**Last Updated:** December 13, 2025  
**Configuration Version:** 1.0
