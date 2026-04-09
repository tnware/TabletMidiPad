using System.ComponentModel;

namespace TabletMidiPad.Services.Wacom;

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
        string? failureHeader = started ? null : $"Service: {failureText ?? "failed to start"}";
        return new WacomServiceStartResult(
            Started: started,
            ShouldRebindInputBackend: started,
            FailureStatusHeader: failureHeader,
            RefreshedStatus: refreshed);
    }

    private static string BuildAdminSuggestionFailureText(string message)
        => $"Could not start Wacom Professional Service. Try running TabletMidiPad as administrator. {message}";
}
