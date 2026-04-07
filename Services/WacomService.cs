using System;
using System.ServiceProcess;

namespace TabletMidiPad.Services;

public sealed class WacomService : IWacomService
{
    private const string ServiceName = "WTabletServicePro";

    public (string HeaderText, bool CanStart) GetStatus()
    {
        try
        {
            using var service = new ServiceController(ServiceName);
            string displayName = string.IsNullOrWhiteSpace(service.DisplayName) ? ServiceName : service.DisplayName;
            bool running = service.Status == ServiceControllerStatus.Running;
            string header = running
                ? $"Service: {displayName} running"
                : $"Service: {displayName} {service.Status}";
            return (header, !running);
        }
        catch (InvalidOperationException)
        {
            return ("Service: not found", false);
        }
        catch (Exception ex)
        {
            return ($"Service: {ex.Message}", false);
        }
    }

    public void Start()
    {
        using var service = new ServiceController(ServiceName);
        if (service.Status == ServiceControllerStatus.Running)
        {
            return;
        }

        service.Start();
        service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
    }
}
