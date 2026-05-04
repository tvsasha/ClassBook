using System.Globalization;

namespace ClassBook.Application.Common
{
    public static class QueryDateParser
    {
        private const string DateFormat = "yyyy-MM-dd";
        private const string FormatErrorMessage = "Некорректный формат даты. Используйте формат: YYYY-MM-DD";

        public static DateTime ParseDateOrDefault(string? value, Func<DateTime> defaultFactory)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultFactory().Date;
            }

            if (DateTime.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return parsed.Date;
            }

            throw new ArgumentException(FormatErrorMessage);
        }

        public static (DateTime Start, DateTime End) ParseRangeOrDefault(
            string? startDate,
            string? endDate,
            Func<DateTime> defaultStartFactory,
            Func<DateTime> defaultEndFactory)
        {
            var start = ParseDateOrDefault(startDate, defaultStartFactory);
            var end = ParseDateOrDefault(endDate, defaultEndFactory).AddDays(1).AddTicks(-1);

            if (start > end)
            {
                throw new ArgumentException("Дата начала не может быть позже даты конца");
            }

            return (start, end);
        }
    }
}
