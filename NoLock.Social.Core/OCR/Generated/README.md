# Mistral OCR Client Generation

## Overview
This directory contains the swagger-generated client for the Mistral OCR API.

## Generation Steps

### Option 1: Using NSwag CLI

1. Install NSwag CLI globally:
```bash
dotnet tool install -g NSwag.ConsoleCore
```

2. Generate the client using the config file:
```bash
nswag run nswag.json
```

### Option 2: Using OpenAPI Generator

1. Install OpenAPI Generator:
```bash
brew install openapi-generator
# or
npm install @openapitools/openapi-generator-cli -g
```

2. Generate the client:
```bash
openapi-generator generate \
  -i https://nolock-ocr-services-qbhx5.ondigitalocean.app/swagger/v1/swagger.json \
  -g csharp-netcore \
  -o . \
  --additional-properties=packageName=NoLock.Social.Core.OCR.Generated,\
  targetFramework=net8.0,\
  nullableReferenceTypes=true,\
  useNewtonsoft=false,\
  netCoreProjectFile=true
```

### Option 3: Using Visual Studio

1. Right-click on the project in Solution Explorer
2. Select "Add" > "Connected Service"
3. Choose "OpenAPI"
4. Enter the swagger URL: `https://nolock-ocr-services-qbhx5.ondigitalocean.app/swagger/v1/swagger.json`
5. Configure settings and generate

## Generated Files

After generation, you should have:
- `MistralOCRClient.cs` - The main client class
- Model classes for request/response objects
- Exception classes for API errors

## Usage Example

```csharp
// In your Startup.cs or Program.cs
services.AddHttpClient<MistralOCRClient>(client =>
{
    client.BaseAddress = new Uri("https://nolock-ocr-services-qbhx5.ondigitalocean.app");
});

// In your service
public class MyService
{
    private readonly MistralOCRClient _ocrClient;
    
    public MyService(MistralOCRClient ocrClient)
    {
        _ocrClient = ocrClient;
    }
    
    public async Task<string> ProcessReceiptAsync(byte[] imageBytes)
    {
        using var stream = new MemoryStream(imageBytes);
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        
        var result = await _ocrClient.ReceiptsAsync(fileContent);
        return result.TrackingId;
    }
}
```

## Notes

- The API currently supports only Receipt and Check document types
- The API returns 202 Accepted, indicating async processing
- No status endpoint is currently available in the swagger spec
- You may need to implement polling or webhook handling for results

## Troubleshooting

If generation fails:
1. Check that the swagger URL is accessible
2. Ensure you have the latest version of the generation tool
3. Try downloading the swagger.json locally and generating from the file
4. Check for any breaking changes in the API specification