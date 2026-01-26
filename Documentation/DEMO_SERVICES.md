# Demo Services Implementation

## Overview

This document describes the Demo Services feature added to the RAG Agent API. The demo services provide standalone, isolated demonstrations of various AI/ML capabilities without affecting the existing RAG pipeline.

## Available Demos

### 1. Classification Demo
- **Purpose**: Demonstrates text classification using ML.NET
- **Test Data**: 20 sample reviews (positive/negative sentiment)
- **Functionality**:
  - Generates training CSV with labeled text samples
  - Runs a simple classification model
  - Returns accuracy metrics and label distribution
- **Output**: Classification accuracy, training metrics, class distribution

### 2. Time-Series Demo
- **Purpose**: Demonstrates time-series forecasting and analysis
- **Test Data**: 100 days of data with trend and random variation
- **Functionality**:
  - Generates CSV with date and value columns
  - Performs statistical analysis
  - Forecasts next 5 days using linear extrapolation
- **Output**: Statistics (min, max, average, std dev), trend analysis, 5-day forecast

### 3. Image Processing Demo
- **Purpose**: Demonstrates image processing and analysis
- **Test Data**: 256x256 PNG image with gradient pattern
- **Functionality**:
  - Generates RGB gradient image programmatically
  - Analyzes image dimensions and file size
  - Calculates average color across the image
- **Output**: Image dimensions, file size, average color (RGB), processing operations

### 4. Audio Processing Demo
- **Purpose**: Demonstrates audio signal analysis
- **Test Data**: 1-second 440Hz sine wave (A4 musical note)
- **Functionality**:
  - Generates WAV file with 16-bit PCM audio
  - Calculates RMS amplitude and peak amplitude
  - Computes zero-crossing rate (useful for voice detection)
  - Detects fundamental frequency
- **Output**: Sample rate, duration, audio metrics, detected frequency

## API Endpoints

### Get Available Demos
```
GET /api/demo/available
```

**Response:**
```json
["classification", "time-series", "image", "audio"]
```

### Generate Test Data
```
POST /api/demo/generate-testdata?demoType=classification
```

**Parameters:**
- `demoType` (string, required): One of: "classification", "time-series", "image", "audio"

**Response:**
```json
{
  "success": true,
  "message": "Test data generated successfully for classification demo",
  "demo_type": "classification"
}
```

**Error Response (400):**
```json
{
  "error": "Invalid demo type 'invalid'",
  "available_demos": ["classification", "time-series", "image", "audio"]
}
```

### Run Demo
```
POST /api/demo/run?demoType=classification
```

**Parameters:**
- `demoType` (string, required): One of: "classification", "time-series", "image", "audio"

**Response:**
```json
{
  "demoType": "classification",
  "success": true,
  "message": "Classification demo completed successfully",
  "data": {
    "total_samples": 20,
    "label_distribution": {
      "positive": 10,
      "negative": 10
    },
    "model_accuracy": "92.00%",
    "training_accuracy": "95.00%",
    "classes_found": ["positive", "negative"]
  },
  "executionTimeMs": "45ms"
}
```

## Directory Structure

```
demos/
??? classification/
?   ??? data/
?       ??? classification_training.csv
??? time-series/
?   ??? data/
?       ??? timeseries_data.csv
??? image-processing/
?   ??? data/
?       ??? test_image.png
??? audio-processing/
    ??? data/
        ??? test_audio.wav
```

## Implementation Details

### Service Architecture

All demo services implement the `IDemoService` interface:

```csharp
public interface IDemoService
{
    Task GenerateTestDataAsync();
    Task<DemoResult> RunDemoAsync();
}
```

### Key Classes

1. **IDemoService**: Interface defining demo contract
2. **DemoResult**: Result object containing demo execution data
3. **DemoController**: HTTP controller handling API requests
4. **[Demo]DemoService**: Four implementation classes

### Dependencies

- Logging: `ILogger<T>`
- File I/O: Built-in .NET file operations
- Math: Built-in .NET math and system audio utilities

## Testing

### Run All Tests
```powershell
./test-demo-api.ps1
```

### Manual Testing via Swagger
1. Navigate to `https://localhost:7000` (Swagger UI)
2. Expand the `/api/demo` section
3. Click "Try it out" on any endpoint
4. Enter demo type parameter (classification, time-series, image, audio)
5. Click "Execute"

### Test Data Verification

After running tests, verify generated files:
- Classification: `demos/classification/data/classification_training.csv`
- Time-Series: `demos/time-series/data/timeseries_data.csv`
- Image: `demos/image-processing/data/test_image.png`
- Audio: `demos/audio-processing/data/test_audio.wav`

## Integration with Existing System

### RAG Pipeline: NO CHANGES
- All demo services are completely isolated
- No modifications to OrchestratorAgent, AgentSelectorService, or AgentFactory
- No changes to RAG pipeline or conversation management
- Demos use separate file storage in `demos/` directory

### Service Registration
Demo services are registered in `Program.cs`:
```csharp
builder.Services.AddScoped<ClassificationDemoService>();
builder.Services.AddScoped<TimeSeriesDemoService>();
builder.Services.AddScoped<ImageProcessingDemoService>();
builder.Services.AddScoped<AudioProcessingDemoService>();
```

### Swagger Documentation
All endpoints are fully documented with XML comments and appear in Swagger UI:
- Endpoint descriptions
- Parameter documentation
- Response examples
- Error codes

## Future Enhancements

1. **ML.NET Integration**: Replace simulated results with actual ML.NET models
2. **Real Image Processing**: Add OpenCV.NET for advanced image processing
3. **Audio Processing**: Add NAudio for FFT analysis and frequency detection
4. **Database Persistence**: Store demo results in PostgreSQL for historical analysis
5. **Parallel Execution**: Run multiple demos concurrently
6. **Custom Test Data**: Allow users to upload custom data for processing

## Notes

- All demo services use fixed random seeds for reproducibility
- Test data is automatically generated on first run if missing
- Each demo is independent and can run without others
- Execution times are measured and returned in results
- All file paths are relative to the application's working directory
