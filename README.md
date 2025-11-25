# PDFA Conversion Service

A robust ASP.NET Core Web API service that converts PDF files to PDF/A-1b format using Ghostscript. The service is designed to run as a Windows Service or console application, providing a RESTful API for PDF/A conversion with comprehensive error handling, validation, and observability features.

## Features

- ✅ **PDF to PDF/A-1b Conversion** - Converts standard PDFs to PDF/A-1b compliant format
- ✅ **RESTful API** - Clean HTTP API with Swagger/OpenAPI documentation
- ✅ **Input Validation** - FluentValidation for robust request validation
- ✅ **Timeout Management** - Configurable timeouts to prevent hanging processes
- ✅ **Structured Logging** - Correlation IDs for request tracing
- ✅ **Error Handling** - Comprehensive error handling with detailed error messages
- ✅ **Windows Service Support** - Can run as a Windows Service or console app
- ✅ **Health Checks** - Built-in health check endpoint
- ✅ **Request Size Limits** - Configurable limits to prevent resource exhaustion
- ✅ **Process Management** - Proper resource cleanup and process lifecycle management

## Prerequisites

- **.NET 10.0 SDK** or later
- **Ghostscript** (version 10.06.0 or later recommended)
  - Download from: https://www.ghostscript.com/download/gsdnld.html
  - Default installation path: `C:\Program Files\gs\gs10.06.0\bin\gswin64c.exe`
- **Windows OS** (for Windows Service support)

## Installation

### 1. Install Ghostscript

Download and install Ghostscript from the official website. The service will attempt to discover Ghostscript automatically, but you can also configure the path in `appsettings.json`.

### 2. Clone and Build

```bash
git clone <repository-url>
cd PDFAConversionService
dotnet restore
dotnet build
```

### 3. Configure the Service

Edit `appsettings.json` to configure Ghostscript path, port, and other settings:

```json
{
  "Ghostscript": {
    "Version": "10.06.0",
    "ExecutablePath": "C:\\Program Files\\gs\\gs10.06.0\\bin\\gswin64c.exe",
    "TempDirectory": "C:\\temp\\PdfaConversion",
    "BaseParameters": "-dNOPAUSE -dBATCH -dSAFER -sDEVICE=pdfwrite -dPDFA=1 -dPDFACompatibilityPolicy=1 -dCompatibilityLevel=1.4 -dEmbedAllFonts=true -dSubsetFonts=true -sColorConversionStrategy=UseDeviceIndependentColor -sProcessColorModel=DeviceRGB -dDownsampleColorImages=false -dDownsampleGrayImages=false -dDownsampleMonoImages=false -dColorImageFilter=/FlateEncode -dGrayImageFilter=/FlateEncode -dMonoImageFilter=/CCITTFaxEncode ",
    "TimeoutInSeconds": 300
  },
  "ServiceHost": {
    "KestrelListenerPort": 7015
  }
}
```

### 4. Run the Service

**As Console Application:**
```bash
dotnet run
```

**As Windows Service:**
```bash
# Install as Windows Service (requires admin privileges)
sc create PDFAConversionService binPath="C:\path\to\PDFAConversionService.exe"
sc start PDFAConversionService
```

## Configuration

### Ghostscript Settings

| Setting | Description | Default |
|---------|-------------|---------|
| `ExecutablePath` | Full path to Ghostscript executable | `C:\Program Files\gs\gs10.06.0\bin\gswin64c.exe` |
| `Version` | Ghostscript version (used for auto-discovery) | `10.06.0` |
| `TempDirectory` | Directory for temporary files | `%TEMP%\PdfaConversion` |
| `BaseParameters` | Ghostscript command-line parameters | See `appsettings.json` |
| `TimeoutInSeconds` | Maximum execution time for conversion | `300` (5 minutes) |

### Service Host Settings

| Setting | Description | Default |
|---------|-------------|---------|
| `KestrelListenerPort` | Port number for the API | `7015` |

### Logging

Logging is configured in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

Logs are written to:
- **Console** (when running as console app)
- **Windows Event Log** (when running as Windows Service)

## API Documentation

### Base URL

```
http://localhost:7015/api/PdfaConversion
```

### Endpoints

#### Convert PDF to PDF/A

**POST** `/api/PdfaConversion/convert`

Converts a PDF file (provided as base64 string) to PDF/A-1b format.

**Request Headers:**
```
Content-Type: application/json
X-Correlation-ID: <optional-correlation-id>
```

**Request Body:**
```json
{
  "base64Pdf": "JVBERi0xLjQKJeLjz9MKMy..."
}
```

**Success Response (200 OK):**
```json
{
  "success": true,
  "base64PdfA": "JVBERi0xLjQKJeLjz9MKMy...",
  "errorMessage": ""
}
```

**Error Responses:**

| Status Code | Description |
|-------------|-------------|
| `400 Bad Request` | Invalid request (missing/invalid base64, size exceeded) |
| `500 Internal Server Error` | Conversion failed |
| `504 Gateway Timeout` | Conversion timed out |

**Example using cURL:**
```bash
curl -X POST "http://localhost:7015/api/PdfaConversion/convert" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: my-request-123" \
  -d '{"base64Pdf":"JVBERi0xLjQKJeLjz9MKMy..."}'
```

**Example using PowerShell:**
```powershell
$base64Pdf = [Convert]::ToBase64String([IO.File]::ReadAllBytes("input.pdf"))
$body = @{ base64Pdf = $base64Pdf } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:7015/api/PdfaConversion/convert" `
  -Method POST `
  -ContentType "application/json" `
  -Body $body
```

#### Health Check

**GET** `/api/PdfaConversion/health`

Returns the health status of the service.

**Response (200 OK):**
```json
{
  "status": "Healthy",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

### Swagger UI

When running in Development mode, Swagger UI is available at:
```
http://localhost:7015/swagger
```

## Architecture

### Service Abstractions

The service uses a clean architecture with the following abstractions:

- **`IFileService`** - Manages temporary file operations
- **`IProcessService`** - Handles external process execution with timeout support
- **`IPdfaConversionService`** - Core conversion logic
- **`ICommandExecutorService`** - Executes system commands (for Ghostscript discovery)

### Key Components

1. **Controllers** - Handle HTTP requests and responses
2. **Services** - Business logic and external process management
3. **Models** - Data transfer objects
4. **Validators** - FluentValidation rules for input validation
5. **Middleware** - Correlation ID tracking for request tracing

### Request Flow

```
Client Request
    ↓
CorrelationIdMiddleware (adds correlation ID)
    ↓
PdfaConversionController (validates request)
    ↓
PdfaConversionService (orchestrates conversion)
    ↓
FileService (manages temp files)
    ↓
ProcessService (executes Ghostscript)
    ↓
Response with converted PDF
```

## Development

### Project Structure

```
PDFAConversionService/
├── Controllers/          # API controllers
├── Services/             # Business logic and abstractions
├── Models/               # Data models
├── Validators/           # FluentValidation validators
├── Middleware/           # HTTP middleware
├── Program.cs            # Application entry point
└── appsettings.json      # Configuration
```

### Building

```bash
dotnet build
```

### Running Tests

```bash
dotnet test
```

### Code Quality

The project follows best practices:
- Dependency Injection
- Interface-based design
- Comprehensive error handling
- Structured logging
- Input validation
- Resource cleanup

## Limitations

- **Maximum File Size**: 100 MB (configurable via `MaxInputSizeBytes` constant)
- **Platform**: Windows only (due to Windows Service support and Ghostscript Windows executable)
- **Timeout**: Default 5 minutes (configurable)

## Troubleshooting

### Ghostscript Not Found

**Error**: `Ghostscript executable not found`

**Solutions**:
1. Install Ghostscript from https://www.ghostscript.com/download/gsdnld.html
2. Configure `Ghostscript:ExecutablePath` in `appsettings.json`
3. Ensure Ghostscript is in the system PATH

### Conversion Timeout

**Error**: `Conversion timed out`

**Solutions**:
1. Increase `TimeoutInSeconds` in `appsettings.json`
2. Check if the PDF is too large or complex
3. Verify Ghostscript parameters are correct

### Port Already in Use

**Error**: `Address already in use`

**Solutions**:
1. Change `KestrelListenerPort` in `appsettings.json`
2. Stop the service using the port
3. Check for other instances of the service running

### Temp Directory Issues

**Error**: `Failed to create temp directory`

**Solutions**:
1. Ensure the service has write permissions to the temp directory
2. Configure a different `TempDirectory` in `appsettings.json`
3. Check available disk space

### Large File Errors

**Error**: `Input PDF size exceeds maximum allowed size`

**Solutions**:
1. The current limit is 100 MB
2. Modify `MaxInputSizeBytes` constant in `PdfaConversionService.cs` if needed
3. Consider splitting large PDFs before conversion

## Security Considerations

- **Input Validation**: All inputs are validated using FluentValidation
- **Request Size Limits**: Kestrel is configured with request size limits (120 MB)
- **Process Isolation**: External processes are properly isolated and cleaned up
- **Temporary Files**: Temporary files are automatically cleaned up after conversion
- **Error Messages**: Error messages don't expose sensitive system information

## Logging and Observability

### Correlation IDs

Every request receives a correlation ID that:
- Is included in response headers (`X-Correlation-ID`)
- Is added to all log entries for that request
- Can be provided by the client in request headers

### Log Levels

- **Information**: Normal operations, conversion start/end
- **Warning**: Non-critical issues (file cleanup failures)
- **Error**: Conversion failures, timeouts, exceptions
- **Debug**: Detailed file operations, process execution details

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Ensure all tests pass
5. Submit a pull request

## License

See LICENSE.txt for details.

## Support

For issues, questions, or contributions, please open an issue in the repository.

## Version History

- **1.0.0** - Initial release with core conversion functionality
  - PDF to PDF/A-1b conversion
  - RESTful API
  - Windows Service support
  - Input validation
  - Timeout management
  - Structured logging with correlation IDs
