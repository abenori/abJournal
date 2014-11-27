using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;
using ablib;

namespace abJournal {
    class ColorBrushConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            return new SolidColorBrush((Color) value);
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            throw new NotImplementedException();
            //return Binding.DoNothing;
        }
    }

    class IsInkingModeConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            return ((InkManipulationMode) value == InkManipulationMode.Inking);
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    class IsErasingModeConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            return ((InkManipulationMode) value == InkManipulationMode.Erasing);
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    class IsSelectingModeCnverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            return ((InkManipulationMode) value == InkManipulationMode.Selecting);
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    class PlusOneConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            return (((int) value) + 1).ToString();
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            try { return Int32.Parse((string) value) - 1; }
            catch(FormatException) { return 0; }
        }
    }

    class MakeDashArrayConverter : IValueConverter {
        DoubleCollection normal = ablib.InkCanvasCollection.DashArray_Normal;
        DoubleCollection dashed = ablib.InkCanvasCollection.DashArray_Dashed;
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            return ((bool) value) ? dashed : normal;
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
