using System.Diagnostics;

namespace vCamService.App.Services;

public class ResourceMonitor : IDisposable
{
    private readonly Process _process = Process.GetCurrentProcess();
    private TimeSpan _lastCpuTime;
    private DateTime _lastSampleTime;

    public ResourceMonitor()
    {
        _lastCpuTime = _process.TotalProcessorTime;
        _lastSampleTime = DateTime.UtcNow;
    }

    public double GetCpuPercent()
    {
        _process.Refresh();
        var cpuUsed = _process.TotalProcessorTime - _lastCpuTime;
        var elapsed = DateTime.UtcNow - _lastSampleTime;
        _lastCpuTime = _process.TotalProcessorTime;
        _lastSampleTime = DateTime.UtcNow;
        if (elapsed.TotalMilliseconds < 1) return 0;
        return cpuUsed.TotalMilliseconds / (elapsed.TotalMilliseconds * Environment.ProcessorCount) * 100.0;
    }

    public double GetRamMb()
    {
        _process.Refresh();
        return _process.WorkingSet64 / (1024.0 * 1024.0);
    }

    public void Dispose() => _process.Dispose();
}
