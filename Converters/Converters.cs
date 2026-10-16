using System.Globalization;
using AsistenteIA.ViewModels;

namespace AsistenteIA.Converters;

/// <summary>Color del mensaje de estado según su tipo y el tema claro/oscuro.</summary>
public class StatusKindToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        return value switch
        {
            StatusKind.Success => Color.FromArgb(dark ? "#7FD99A" : "#1B7F3B"),
            StatusKind.Error => Color.FromArgb(dark ? "#F2B8B5" : "#B3261E"),
            StatusKind.Busy => Color.FromArgb(dark ? "#F5A25D" : "#8A5300"),
            _ => Color.FromArgb(dark ? "#C8C8C8" : "#404040")
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class InvertBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : value;
}

/// <summary>true si la cadena no está vacía (para mostrar u ocultar etiquetas).</summary>
public class HasTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        !string.IsNullOrWhiteSpace(value as string);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Color de la etiqueta de verificación (verde verificada / naranja sin verificar).</summary>
public class VerifiedToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        return value is true
            ? Color.FromArgb(dark ? "#7FD99A" : "#1B7F3B")
            : Color.FromArgb(dark ? "#F5A25D" : "#8A5300");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
