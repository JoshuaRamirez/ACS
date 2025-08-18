# ACS Tenant Process Monitor Dashboard

A real-time console-based monitoring dashboard for ACS (Access Control System) tenant processes, designed for Windows environments with Unix-style UI patterns.

## Features

- **Real-time Monitoring**: Live updates every 2 seconds with system and tenant metrics
- **Multi-Panel Layout**: Overview, tenant details, and system metrics views
- **Interactive Navigation**: Keyboard shortcuts for switching views and filtering data
- **Windows-Compatible**: Uses Windows Console API with Unicode box-drawing characters
- **Tenant-Focused**: Per-tenant process monitoring with health status tracking

## Dashboard Views

### Overview View (F1)
```
┌─ ACS Tenant Process Monitor ────────────────────────────────────┐
│ System: CPU 45% | RAM 2.1GB/8GB | Network ↑15MB/s ↓8MB/s      │
├─────────────────────────────────────────────────────────────────┤
│ Active Tenants: 12 | Healthy: 10 | Degraded: 2 | Failed: 0     │
├─────────────────────────────────────────────────────────────────┤
│ TenantID     │ PID   │ CPU% │ RAM   │ Req/s │ gRPC │ Status    │
│ tenant-001   │ 1234  │ 12.5 │ 156MB │   45  │ OK   │ Healthy   │
│ tenant-002   │ 1235  │  8.3 │ 142MB │   32  │ OK   │ Healthy   │
│ tenant-003   │ 1236  │ 25.1 │ 203MB │   78  │ SLOW │ Degraded  │
├─────────────────────────────────────────────────────────────────┤
│ [F1] Help [F2] Details [F3] System [F4] Refresh [ESC] Exit     │
└─────────────────────────────────────────────────────────────────┘
```

### Tenant Details View (F2)
Shows detailed metrics for a specific tenant including:
- Process information and health status
- CPU and memory usage
- Request throughput and error rates
- gRPC communication status
- Last health check timestamp

### System Metrics View (F3)
Displays system-wide performance metrics:
- Overall CPU usage
- Memory consumption and availability
- Active connections count
- System uptime
- Network throughput

## Usage

### Standalone Dashboard Application

Run the standalone dashboard to monitor multiple tenant processes:

```bash
dotnet run --project ACS.Dashboard
```

### Integrated with VerticalHost

Enable the dashboard within a VerticalHost process by updating `appsettings.json`:

```json
{
  "Dashboard": {
    "Enabled": true,
    "RefreshIntervalMs": 2000
  }
}
```

Then run the VerticalHost:

```bash
dotnet run --project ACS.VerticalHost --tenant your-tenant-id
```

## Keyboard Controls

- **F1**: Switch to Overview view
- **F2**: Switch to Tenant Details view
- **F3**: Switch to System Metrics view
- **F4**: Force refresh data
- **ESC**: Exit dashboard
- **Ctrl+Q**: Quick exit

## Configuration

Configure the dashboard behavior in `appsettings.json`:

```json
{
  "Dashboard": {
    "Enabled": true,
    "RefreshIntervalMs": 2000,
    "ConsoleWidth": 120,
    "ConsoleHeight": 30,
    "ShowDetailedMetrics": true,
    "ShowSystemMetrics": true,
    "EnableKeyboardShortcuts": true,
    "ColorTheme": "Default",
    "LogEvents": false
  },
  "PerformanceMetrics": {
    "CollectionIntervalMs": 5000,
    "EnableSystemMetrics": true,
    "EnableApplicationMetrics": true,
    "EnableGrpcMetrics": true
  }
}
```

### Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `Enabled` | `false` | Enable/disable the console dashboard |
| `RefreshIntervalMs` | `2000` | Dashboard refresh interval in milliseconds |
| `ConsoleWidth` | `120` | Console width for dashboard rendering |
| `ConsoleHeight` | `30` | Console height for dashboard rendering |
| `ShowDetailedMetrics` | `true` | Show detailed tenant metrics |
| `ShowSystemMetrics` | `true` | Show system-level metrics |
| `EnableKeyboardShortcuts` | `true` | Enable interactive keyboard controls |
| `ColorTheme` | `"Default"` | Color theme (currently only Default) |
| `LogEvents` | `false` | Log dashboard events for debugging |

## Architecture

The dashboard system consists of:

- **ConsoleDashboardService**: Main background service that renders the dashboard
- **DashboardExtensions**: Configuration and service registration helpers
- **MultiTenantDiscoveryService**: Discovers and tracks tenant processes
- **Performance Integration**: Uses existing PerformanceMetricsService for data

## Development

### Adding New Views

1. Add a new enum value to `DashboardView`
2. Create a render method following the pattern `RenderXxxDashboard(StringBuilder sb)`
3. Add keyboard shortcut handling in `HandleKeyPress(ConsoleKeyInfo key)`

### Metrics Integration

The dashboard integrates with:
- **PerformanceMetricsService**: System and application metrics
- **HealthMonitoringService**: Tenant health status
- **OpenTelemetry**: Distributed tracing and metrics export

## Troubleshooting

### Console Size Issues
- Ensure terminal supports Unicode box-drawing characters
- Minimum recommended size: 100x25 characters
- Use Windows Terminal for best experience

### Missing Metrics
- Verify `PerformanceMetrics:Enabled` is `true`
- Check `PerformanceMetrics:CollectionIntervalMs` setting
- Ensure tenant processes are running with health monitoring enabled

### Keyboard Input Issues
- Dashboard requires exclusive console input
- Don't run multiple dashboard instances simultaneously
- Use `Ctrl+C` if ESC doesn't respond