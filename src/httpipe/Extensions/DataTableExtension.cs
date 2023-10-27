using CsvHelper;
using System.Data;
using System.Globalization;

namespace httpipe.Extensions
{
    internal static class DataTableExtension
    {
        public static DataTable Load(this DataTable context, string csvText)
        {
            using (var reader = new StringReader(csvText))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                using (var data = new CsvDataReader(csv))
                {
                    context.Load(data);
                }
            }

            return context;
        }
    }
}
