using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;

namespace BODA_VISION_AI.Views.ToolSettings
{
    /// <summary>
    /// ToolSettingsView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ToolSettingsView : UserControl
    {
        public ToolSettingsView()
        {
            InitializeComponent();
        }
    }

    /// <summary>
    /// Enum 값을 배열로 변환하는 컨버터
    /// </summary>
    public class EnumToValuesConverter : MarkupExtension, IValueConverter
    {
        private static EnumToValuesConverter? _instance;
        public static EnumToValuesConverter Instance => _instance ??= new EnumToValuesConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Type enumType && enumType.IsEnum)
            {
                return Enum.GetValues(enumType);
            }
            return Array.Empty<object>();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return Instance;
        }
    }
}
