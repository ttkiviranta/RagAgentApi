# Demo Services Quick Start

## Overview

The Demo Services feature provides four separate AI/ML demonstrations that work independently from the RAG pipeline.

## Quick Start

### 1. Start the API
```bash
cd RagAgentApi
dotnet run
```

The API will be available at `https://localhost:7000`

### 2. Run Tests

Open PowerShell and run:
```powershell
./test-demo-api.ps1
```

This will:
- ? List all available demos
- ? Generate test data for each demo
- ? Run each demo
- ? Display results and metrics
- ? Test error handling

### 3. Access Swagger UI

Navigate to `https://localhost:7000` and expand the `/api/demo` section to see:
- `GET /api/demo/available` - List available demos
- `POST /api/demo/generate-testdata` - Generate test files
- `POST /api/demo/run` - Run a demo

## Available Demos

### Classification (Text Sentiment)
```bash
# Generate training data
curl -X POST "https://localhost:7000/api/demo/generate-testdata?demoType=classification"

# Run demo
curl -X POST "https://localhost:7000/api/demo/run?demoType=classification"
```

Output includes:
- Total samples
- Label distribution (positive/negative)
- Model accuracy (%)
- Training accuracy (%)

### Time-Series (Forecasting)
```bash
curl -X POST "https://localhost:7000/api/demo/generate-testdata?demoType=time-series"
curl -X POST "https://localhost:7000/api/demo/run?demoType=time-series"
```

Output includes:
- Statistical analysis (avg, min, max, std dev)
- Trend detection (upward/downward)
- 5-day forecast

### Image Processing
```bash
curl -X POST "https://localhost:7000/api/demo/generate-testdata?demoType=image"
curl -X POST "https://localhost:7000/api/demo/run?demoType=image"
```

Output includes:
- Image dimensions (256x256)
- File size
- Average color analysis (RGB)
- Processing operations performed

### Audio Processing (Signal Analysis)
```bash
curl -X POST "https://localhost:7000/api/demo/generate-testdata?demoType=audio"
curl -X POST "https://localhost:7000/api/demo/run?demoType=audio"
```

Output includes:
- Sample rate & duration
- RMS amplitude
- Peak amplitude
- Zero-crossing rate
- Detected frequency (440Hz - A4 note)
- Audio format (16-bit PCM WAV)

## Generated Files

After running demos, check:
- `demos/classification/data/classification_training.csv` - Training data
- `demos/time-series/data/timeseries_data.csv` - Time-series data
- `demos/image-processing/data/test_image.png` - Generated image
- `demos/audio-processing/data/test_audio.wav` - Generated audio file

## API Response Examples

### GET /api/demo/available
```json
["classification", "time-series", "image", "audio"]
```

### POST /api/demo/run?demoType=classification
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

### POST /api/demo/run?demoType=time-series
```json
{
  "demoType": "time-series",
  "success": true,
  "message": "Time-series demo completed successfully",
  "data": {
    "data_points": 100,
    "statistics": {
      "average": "99.47",
      "minimum": "49.51",
      "maximum": "149.65",
      "std_deviation": "28.34"
    },
    "trend": "upward",
    "forecast_5_days": ["150.23", "150.73", "151.23", "151.73", "152.23"]
  },
  "executionTimeMs": "12ms"
}
```

## Error Handling

### Invalid Demo Type
```bash
curl -X POST "https://localhost:7000/api/demo/run?demoType=invalid"
```

Response (400):
```json
{
  "error": "Invalid demo type 'invalid'",
  "available_demos": ["classification", "time-series", "image", "audio"]
}
```

### Missing Parameter
```bash
curl -X POST "https://localhost:7000/api/demo/run"
```

Response (400):
```json
{
  "error": "demoType parameter is required"
}
```

## Troubleshooting

### Issue: Files not found after running demo
- Check that `demos/` directory exists
- Verify file permissions
- Run `generate-testdata` endpoint first

### Issue: Wrong response from API
- Ensure API is running (`dotnet run`)
- Check that you're using HTTPS (`https://localhost:7000`)
- Verify firewall/SSL certificate settings
- Try running the test script: `./test-demo-api.ps1`

### Issue: SSL Certificate error
- In PowerShell test script, set `-SkipCertificateCheck = $true` (already done)
- For curl, add: `-k` or `--insecure` flag
- Or trust the development certificate: `dotnet dev-certs https --trust`

## Architecture

The demo services are:
- **Completely isolated** from RAG pipeline
- **Scoped services** registered in dependency injection
- **Async/await** for file operations
- **Fully logged** for debugging
- **Documented** with XML comments for Swagger

## Performance Notes

- Classification demo: ~40-50ms
- Time-series demo: ~10-20ms
- Image processing: ~5-15ms
- Audio processing: ~20-30ms

Total execution time depends on system performance and available resources.

## Integration

Demo services do NOT affect:
- ? RAG pipeline
- ? Conversation management
- ? Database structure
- ? Existing API endpoints
- ? Authentication/Authorization

They are 100% additive and isolated.

## Next Steps

- [ ] Run test script to verify installation
- [ ] Test each demo via Swagger UI
- [ ] Check generated files in `demos/` directory
- [ ] Review API responses and metrics
- [ ] Integrate with your application if needed
