using System.Globalization;

namespace MediaGlass;

internal static class AppStrings
{
    private static bool IsPersian => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fa";

    public const string ProductName = "Media Glass Controller - MySystem";

    public static string WaitingTitle => IsPersian ? "در انتظار پخش رسانه…" : "Waiting for media…";

    public static string WaitingSubtitle => IsPersian
        ? "یک ویدیو یا آهنگ را در YouTube پخش کنید"
        : "Play a YouTube video or any media";

    public static string UnknownArtist => IsPersian ? "رسانهٔ در حال پخش" : "Now playing";

    public static string LockPosition => IsPersian ? "قفل‌کردن جای باکس" : "Lock widget position";

    public static string UnlockPosition => IsPersian ? "بازکردن قفل جای باکس" : "Unlock widget position";

    public static string Previous => IsPersian ? "قبلی" : "Previous";

    public static string Next => IsPersian ? "بعدی" : "Next";

    public static string PlayPause => IsPersian ? "پخش یا مکث" : "Play or pause";

    public static string Stop => IsPersian ? "توقف" : "Stop";

    public static string Close => IsPersian ? "بستن" : "Close";

    public static string AlwaysOnTop => IsPersian ? "همیشه روی همهٔ پنجره‌ها" : "Always on top";
}
