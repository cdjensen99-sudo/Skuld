using System.Globalization;
using UnityEngine;

namespace Skuld;

internal static class ModColorUtil
{
    internal static Color Parse(string value, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        string trimmed = value.Trim();
        if (TryParseHex(trimmed, out Color hexColor))
        {
            return hexColor;
        }

        if (TryParseRgb(trimmed, out Color rgbColor))
        {
            return rgbColor;
        }

        Plugin.Log.LogWarning($"Skuld: could not parse color '{value}'. Using default.");
        return fallback;
    }

    private static bool TryParseHex(string value, out Color color)
    {
        color = default;
        string hex = value.StartsWith("#", System.StringComparison.Ordinal) ? value.Substring(1) : value;
        if (hex.Length != 6 && hex.Length != 8)
        {
            return false;
        }

        if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint raw))
        {
            return false;
        }

        if (hex.Length == 6)
        {
            color = new Color(
                ((raw >> 16) & 0xFF) / 255f,
                ((raw >> 8) & 0xFF) / 255f,
                (raw & 0xFF) / 255f,
                1f);
            return true;
        }

        color = new Color(
            ((raw >> 24) & 0xFF) / 255f,
            ((raw >> 16) & 0xFF) / 255f,
            ((raw >> 8) & 0xFF) / 255f,
            (raw & 0xFF) / 255f);
        return true;
    }

    private static bool TryParseRgb(string value, out Color color)
    {
        color = default;
        string[] parts = value.Split(',');
        if (parts.Length < 3 || parts.Length > 4)
        {
            return false;
        }

        if (!TryParseByte(parts[0], out byte r) ||
            !TryParseByte(parts[1], out byte g) ||
            !TryParseByte(parts[2], out byte b))
        {
            return false;
        }

        float a = 1f;
        if (parts.Length == 4)
        {
            if (!TryParseByte(parts[3], out byte alphaByte))
            {
                return false;
            }

            a = alphaByte / 255f;
        }

        color = new Color(r / 255f, g / 255f, b / 255f, a);
        return true;
    }

    private static bool TryParseByte(string value, out byte result)
    {
        if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            result = (byte)Mathf.Clamp(parsed, 0, 255);
            return true;
        }

        result = 0;
        return false;
    }
}
