namespace SmartHome.Backend.Extensions;

public static class DateTimeExtensions
{
    /// <summary>
    /// Restituisce una versione UTC del DateTime.
    /// Unspecified viene interpretato come già UTC (no conversione) a meno che non si voglia cambiare comportamento.
    /// </summary>
    public static DateTime EnsureUtc(this DateTime dt, bool unspecifiedIsUtc = true)
    {
        return dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            DateTimeKind.Unspecified => unspecifiedIsUtc ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : DateTime.SpecifyKind(dt, DateTimeKind.Local).ToUniversalTime(),
            _ => dt
        };
    }
}
