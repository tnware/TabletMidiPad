using System;
using System.ComponentModel;

namespace TabletMidiPad.Services;

/// <summary>
/// Default coordinator for Wacom service startup/recovery decisions.
/// </summary>
internal sealed class WacomServiceWorkflowCoordinator : IWacomServiceWorkflowCoordinator
{
    /// <inheritdoc />
    public WacomServiceWorkflowState BuildStartingState()
    {
        return new WacomServiceWorkflowState(
            StatusHeader: "Service: starting...",
            StartEnabled: false);
    }

    /// <inheritdoc />
    public WacomServiceStartResult Start(IWacomServiceController serviceController)
    {
        bool started = false;
        string? failureText = null;
        try
        {
            serviceController.Start();
            started = true;
        }
        catch (Win32Exception ex)
        {
            failureText = BuildAdminSuggestionFailureText(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            failureText = BuildAdminSuggestionFailureText(ex.Message);
        }
        catch (Exception ex)
        {
            failureText = $"Could not start Wacom Professional Service: {ex.Message}";
        }

        WacomServiceStatus refreshed = serviceController.GetStatus();
        return new WacomServiceStartResult(
            Started: started,
            ShouldRebindInputBackend: started,
            FailureStatusHeader: started ? null : $"Service: {(failureText ?? "failed to start")}",
            RefreshedStatus: refreshed);
    }

    private static string BuildAdminSuggestionFailureText(string message)
        => $"Could not start Wacom Professional Service. Try running TabletMidiPad as administrator. {message}";
}
