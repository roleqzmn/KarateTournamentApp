using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media;

namespace KarateTournamentApp.Converters
{
    public class DiscardedScoreBackgroundConverter : IMultiValueConverter
    {
        private static readonly Brush DefaultBrush = new SolidColorBrush(Color.FromRgb(0x95, 0xA5, 0xA6));
        private static readonly Brush DiscardedBrush = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C));

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2)
            {
                return DefaultBrush;
            }

            if (values[0] is not int index)
            {
                return DefaultBrush;
            }

            if (values[1] is IEnumerable discardedIndexes)
            {
                foreach (var item in discardedIndexes.Cast<object>())
                {
                    if (item is int discardedIndex && discardedIndex == index)
                    {
                        return DiscardedBrush;
                    }
                }
            }

            return DefaultBrush;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
