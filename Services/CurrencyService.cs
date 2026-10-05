using System.Globalization;

namespace BistroPOS.Mobile.Services
{
    public static class CurrencyService
    {
        private const string SymbolKey = "CurrencySymbol";

        public static string Symbol => Preferences.Get(SymbolKey, "ل.ل");

        public static void SetSymbol(string? symbol)
        {
            if (!string.IsNullOrWhiteSpace(symbol))
                Preferences.Set(SymbolKey, symbol);
        }

        public static string FormatNumber(decimal v)
        {
            return v == Math.Truncate(v)
                ? v.ToString("#,##0", CultureInfo.InvariantCulture)
                : v.ToString("#,##0.00", CultureInfo.InvariantCulture);
        }

        // رموز فيها حروف (ل.ل، USD) بتنكتب بعد الرقم، ورموز متل $ بتنكتب قبله
        public static string Format(decimal v)
        {
            string n = FormatNumber(v);
            string s = Symbol;
            bool suffix = s.Any(char.IsLetter);
            return suffix ? $"{n} {s}" : $"{s}{n}";
        }

        // قراءة رقم مكتوب بالتلفون (بيقبل نقطة أو فاصلة عشرية) بغض النظر عن لغة التلفون
        public static bool TryParse(string? text, out decimal value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string t = text.Trim().Replace("٫", ".").Replace(",", ".");
            return decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }
    }
}
