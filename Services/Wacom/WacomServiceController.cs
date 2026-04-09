using System;
using System.ComponentModel;
using System.ServiceProcess;

namespace TabletMidiPad.Services.Wacom;

/// <summary>
/// Default implementation for interacting with the Wacom Professional Service.
/// </summary>
internal sealed class WacomServiceController : IWacomServiceController
{
    private const string WacomServiceName = "WTabletServicePro";

    public WacomServiceStatus GetStatus()
    {
        try
        {
            using var service = new ServiceController(WacomServiceName);
            string displayName = string.IsNullOrWhiteSpace(service.DisplayName) ? WacomServiceName : service.DisplayName;
            bool running = service.Status == ServiceControllerStatus.Running;
            string text = running
                ? $"Service: {displayName} running"
                : $"Service: {displayName} {service.Status}";
            return new WacomServiceStatus(text, CanStart: !running, IsServiceRunning: running);
        }
        catch (InvalidOperationException)
        {
            return new WacomServiceStatus("Service: not found", CanStart: false, IsServiceRunning: false);
        }
        catch (Exception ex)
        {
            return new WacomServiceStatus($"Service: {ex.Message}", CanStart: false, IsServiceRunning: false);
        }
    }

    public void Start()
    {
        using var service = new ServiceController(WacomServiceName);
        if (service.Status == ServiceControllerStatus.Running)
        {
            return;
        }

        service.Start();
        service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
    }
}
