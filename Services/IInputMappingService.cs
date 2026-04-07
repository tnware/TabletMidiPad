namespace TabletMidiPad.Services;

public interface IInputMappingService
{
    int ToCc(double normalized, bool invert);
    int ApplyPressureCurve(int pressure, string? pressureCurve);
    double Snap(double value, double increment);
    string NormalizeInputBackendMode(string? text);
    string NormalizeReleaseBehavior(string? text);
    string NormalizePenButtonAction(string? text);
}
