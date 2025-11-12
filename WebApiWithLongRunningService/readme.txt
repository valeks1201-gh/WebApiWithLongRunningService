Usage examples:

# Start a long-running process
POST /api/processing/LongRunning
{
  "data": "Sample processing data",
  "iterations": 15,
  "clientId": "client-123"
}

# Check status
GET /api/processing/status

# Health check
GET /health