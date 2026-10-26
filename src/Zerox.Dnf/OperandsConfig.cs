// ============================================================================
// Zerox.Dnf - OperandsConfig - P80 operand parsing (Cx.Fy format)
//
// Copyright (c) 2025 3U Vision s.r.l. / Ladislav Sopko
// All rights reserved.
// ============================================================================

namespace Zerox.Dnf;

/// <summary>
/// P80-specific operand parsing for Cx.Fy format.
/// Hardcoded for 4 cameras (C1-C4) and 16 filters (F0-F15).
/// </summary>
public static class OperandsConfig
{
    public const int NumCameras = 4;
    public const int FiltersPerCamera = 16;

    /// <summary>
    /// Try to parse Cx.Fy format operand name.
    /// Returns camera (1-4) and filter (0-15).
    /// </summary>
    public static bool TryParseBitRef(string operand, out int camera, out int filter)
    {
        camera = 0;
        filter = 0;

        if (string.IsNullOrEmpty(operand) || operand.Length < 5)
            return false;

        // Expected format: Cx.Fy where x=1-4, y=0-15
        if (operand[0] != 'C' || !char.IsDigit(operand[1]))
            return false;

        int dotIdx = operand.IndexOf('.');
        if (dotIdx < 2 || dotIdx >= operand.Length - 1)
            return false;

        if (operand[dotIdx + 1] != 'F')
            return false;

        string camPart = operand.Substring(1, dotIdx - 1);
        string filtPart = operand.Substring(dotIdx + 2);

        if (!int.TryParse(camPart, out camera) || !int.TryParse(filtPart, out filter))
            return false;

        return camera >= 1 && camera <= NumCameras
            && filter >= 0 && filter < FiltersPerCamera;
    }
}
