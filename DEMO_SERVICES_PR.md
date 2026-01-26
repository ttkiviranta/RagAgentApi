# Pull Request: Add Demo Services for AI/ML Demonstrations

## Description

This PR adds a complete Demo Services feature to the RAG Agent API that provides isolated, standalone demonstrations of various AI/ML capabilities. The demo services are completely separate from the existing RAG pipeline and do not affect any core functionality.

## Changes

### New Files Added

#### Service Layer
- **`Services/DemoServices/IDemoService.cs`**: Interface defining the demo service contract
- **`Services/DemoServices/ClassificationDemoService.cs`**: Text classification demo
- **`Services/DemoServices/TimeSeriesDemoService.cs`**: Time-series forecasting demo
- **`Services/DemoServices/ImageProcessingDemoService.cs`**: Image processing demo
- **`Services/DemoServices/AudioProcessingDemoService.cs`**: Audio signal analysis demo

#### Controller
- **`Controllers/DemoController.cs`**: HTTP API endpoints for demo management

#### Documentation
- **`Documentation/DEMO_SERVICES.md`**: Complete feature documentation
- **`test-demo-api.ps1`**: Comprehensive PowerShell test script

### Modified Files

- **`Program.cs`**: Added namespace import and DI service registration for demo services

## Features Added

### API Endpoints

1. **GET /api/demo/available**
   - Returns list of available demo types
   - Response: `["classification", "time-series", "image", "audio"]`

2. **POST /api/demo/generate-testdata?demoType={type}**
   - Generates test data for specified demo type
   - Creates necessary directories and test files
   - Supports: classification, time-series, image, audio

3. **POST /api/demo/run?demoType={type}**
   - Executes specified demo and returns results
   - Automatically generates test data if missing
   - Returns metrics and analysis results

### Demo Types

#### Classification Demo
- Generates 20 training samples (positive/negative sentiment)
- Returns classification accuracy and label distribution
- Output format: CSV training data

#### Time-Series Demo
- Generates 100 days of time-series data with trend
- Performs statistical analysis
- Forecasts next 5 days
- Output format: CSV with date and value columns

#### Image Processing Demo
- Generates 256x256 PNG image with RGB gradient
- Analyzes image properties and colors
- Returns dimensions, file size, and color analysis
- Output format: PNG image file

#### Audio Processing Demo
- Generates 1-second 440Hz sine wave (A4 note)
- Calculates RMS and peak amplitude
- Computes zero-crossing rate
- Returns audio metrics and format info
- Output format: 16-bit PCM WAV file

### Directory Structure

```
demos/
??? classification/data/
?   ??? classification_training.csv
??? time-series/data/
?   ??? timeseries_data.csv
??? image-processing/data/
?   ??? test_image.png
??? audio-processing/data/
    ??? test_audio.wav
```

## Testing

### Test Script
```powershell
./test-demo-api.ps1
```

This script:
- ? Gets available demos
- ? Generates test data for each demo type
- ? Runs each demo and validates results
- ? Tests error handling (invalid types, missing parameters)
- ? Verifies response formats and content

### Manual Testing via Swagger
1. Navigate to `https://localhost:7000`
2. Expand `/api/demo` section
3. Use "Try it out" buttons to test endpoints
4. View sample responses and error handling

### Test Coverage

- All endpoints tested with valid and invalid inputs
- Error handling verified for:
  - Invalid demo types
  - Missing required parameters
  - File generation and processing
- Response format validation
- Execution time tracking

## API Documentation

### Swagger Integration
- All endpoints fully documented with XML comments
- Parameter descriptions and validation rules included
- Response examples provided
- Error codes and descriptions documented

### Example Requests

```bash
# Get available demos
curl https://localhost:7000/api/demo/available

# Generate test data
curl -X POST https://localhost:7000/api/demo/generate-testdata?demoType=classification

# Run demo
curl -X POST https://localhost:7000/api/demo/run?demoType=classification
```

## Impact Analysis

### ? No Changes to Existing Systems

- **RAG Pipeline**: Completely untouched
  - OrchestratorAgent: No changes
  - AgentSelectorService: No changes
  - AgentFactory: No changes
  - QueryAgent: No changes
  - PostgresQueryAgent: No changes

- **Conversation Management**: No changes
  - ConversationService: No changes
  - ConversationController: No changes

- **Database**: No changes
  - No new tables or migrations
  - No existing data modified

- **Other Services**: No changes
  - Azure services untouched
  - PostgreSQL services untouched
  - Existing agents untouched

### ? New Functionality Only

- Completely isolated demo services
- New `/api/demo` endpoint route
- Separate file storage in `demos/` directory
- No dependencies on RAG pipeline

## Performance Considerations

- Demo services are scoped (created per request)
- File I/O operations are async
- Execution times are tracked and returned
- Test data generation is optimized for demo scenarios
- No blocking operations or heavy computations

## Future Enhancements

Potential future improvements (not in this PR):
- ML.NET integration for actual model training
- Advanced image processing with OpenCV.NET
- FFT analysis for audio processing
- PostgreSQL persistence for demo results
- Parallel demo execution
- Custom test data upload endpoint

## Verification Checklist

- [x] All demo services implemented
- [x] API endpoints created and tested
- [x] Swagger documentation complete
- [x] Test script provided and validated
- [x] Error handling implemented
- [x] File operations working correctly
- [x] No changes to RAG pipeline
- [x] No database migrations needed
- [x] Code follows existing patterns
- [x] Logging implemented for debugging
- [x] XML comments added for Swagger
- [x] Build successful (no compilation errors)

## Related Issues

N/A - This is a new feature

## Breaking Changes

None - This is purely additive

## Dependencies Added

None - Uses only built-in .NET functionality

## Migration Steps

None required - Demo services are standalone
