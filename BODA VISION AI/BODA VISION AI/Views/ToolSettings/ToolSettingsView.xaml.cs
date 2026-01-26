using BODA_VISION_AI.Controls;
using BODA_VISION_AI.Models;
using BODA_VISION_AI.VisionTools.BlobAnalysis;
using BODA_VISION_AI.VisionTools.ImageProcessing;
using BODA_VISION_AI.VisionTools.Measurement;
using BODA_VISION_AI.VisionTools.PatternMatching;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace BODA_VISION_AI.Views.ToolSettings
{
    public partial class ToolSettingsView : UserControl
    {
        private string _currentToolType = "";

        public ToolSettingsView()
        {
            InitializeComponent();
        }

        private void UserControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            ParametersPanel.Children.Clear();

            if (DataContext is VisionToolBase tool)
            {
                _currentToolType = tool.ToolType;
                AddToolHeader(tool);
                BuildParameterUI(tool);
            }
        }

        private void AddToolHeader(VisionToolBase tool)
        {
            var headerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 10)
            };

            var titleText = new TextBlock
            {
                Text = tool.Name,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD700")),
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerPanel.Children.Add(titleText);

            // 도구 도움말 아이콘
            var helpIcon = new HelpIcon
            {
                ToolType = tool.ToolType,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            headerPanel.Children.Add(helpIcon);

            ParametersPanel.Children.Add(headerPanel);
        }

        private void BuildParameterUI(VisionToolBase tool)
        {
            switch (tool)
            {
                case BlurTool blurTool:
                    BuildBlurToolUI(blurTool);
                    break;
                case ThresholdTool thresholdTool:
                    BuildThresholdToolUI(thresholdTool);
                    break;
                case EdgeDetectionTool edgeTool:
                    BuildEdgeDetectionToolUI(edgeTool);
                    break;
                case MorphologyTool morphTool:
                    BuildMorphologyToolUI(morphTool);
                    break;
                case HistogramTool histTool:
                    BuildHistogramToolUI(histTool);
                    break;
                case TemplateMatchTool templateTool:
                    BuildTemplateMatchToolUI(templateTool);
                    break;
                case FeatureMatchTool featureTool:
                    BuildFeatureMatchToolUI(featureTool);
                    break;
                case BlobTool blobTool:
                    BuildBlobToolUI(blobTool);
                    break;
                case CaliperTool caliperTool:
                    BuildCaliperToolUI(caliperTool);
                    break;
                case LineFitTool lineTool:
                    BuildLineFitToolUI(lineTool);
                    break;
                case CircleFitTool circleTool:
                    BuildCircleFitToolUI(circleTool);
                    break;
                case GrayscaleTool:
                    AddLabel("No additional parameters");
                    break;
                default:
                    AddLabel("No parameters available");
                    break;
            }
        }

        #region Blur Tool
        private void BuildBlurToolUI(BlurTool tool)
        {
            AddEnumComboBox<BlurType>("Blur Type", tool, nameof(BlurTool.BlurType));
            AddSlider("Kernel Size", tool, nameof(BlurTool.KernelSize), 1, 31, 2, true);
            AddSlider("Sigma X", tool, nameof(BlurTool.SigmaX), 0, 10, 0.5);
            AddSlider("Sigma Y", tool, nameof(BlurTool.SigmaY), 0, 10, 0.5);
        }
        #endregion

        #region Threshold Tool
        private void BuildThresholdToolUI(ThresholdTool tool)
        {
            AddSlider("Threshold Value", tool, nameof(ThresholdTool.ThresholdValue), 0, 255, 1);
            AddSlider("Max Value", tool, nameof(ThresholdTool.MaxValue), 0, 255, 1);
            AddCheckBox("Use Otsu", tool, nameof(ThresholdTool.UseOtsu));
            AddCheckBox("Use Adaptive", tool, nameof(ThresholdTool.UseAdaptive));
            AddSlider("Block Size", tool, nameof(ThresholdTool.BlockSize), 3, 51, 2, true);
            AddSlider("Constant C", tool, nameof(ThresholdTool.CValue), -20, 20, 1);
        }
        #endregion

        #region Edge Detection Tool
        private void BuildEdgeDetectionToolUI(EdgeDetectionTool tool)
        {
            AddEnumComboBox<EdgeDetectionMethod>("Method", tool, nameof(EdgeDetectionTool.Method));
            AddSlider("Canny Threshold 1", tool, nameof(EdgeDetectionTool.CannyThreshold1), 0, 255, 1);
            AddSlider("Canny Threshold 2", tool, nameof(EdgeDetectionTool.CannyThreshold2), 0, 255, 1);
            AddSlider("Aperture Size", tool, nameof(EdgeDetectionTool.CannyApertureSize), 3, 7, 2, true);
            AddCheckBox("L2 Gradient", tool, nameof(EdgeDetectionTool.L2Gradient));
        }
        #endregion

        #region Morphology Tool
        private void BuildMorphologyToolUI(MorphologyTool tool)
        {
            AddEnumComboBox<MorphologyOperation>("Operation", tool, nameof(MorphologyTool.Operation));
            AddSlider("Kernel Width", tool, nameof(MorphologyTool.KernelWidth), 1, 21, 1);
            AddSlider("Kernel Height", tool, nameof(MorphologyTool.KernelHeight), 1, 21, 1);
            AddSlider("Iterations", tool, nameof(MorphologyTool.Iterations), 1, 10, 1);
        }
        #endregion

        #region Histogram Tool
        private void BuildHistogramToolUI(HistogramTool tool)
        {
            AddEnumComboBox<HistogramOperation>("Operation", tool, nameof(HistogramTool.Operation));
            AddSlider("Clip Limit", tool, nameof(HistogramTool.ClipLimit), 1, 10, 0.5);
            AddSlider("Tile Grid Width", tool, nameof(HistogramTool.TileGridWidth), 1, 16, 1);
            AddSlider("Tile Grid Height", tool, nameof(HistogramTool.TileGridHeight), 1, 16, 1);
        }
        #endregion

        #region Template Match Tool
        private void BuildTemplateMatchToolUI(TemplateMatchTool tool)
        {
            AddSlider("Match Threshold", tool, nameof(TemplateMatchTool.MatchThreshold), 0, 1, 0.05);
            AddSlider("Max Results", tool, nameof(TemplateMatchTool.MaxResults), 1, 50, 1);
            AddCheckBox("Enable Multi-Scale", tool, nameof(TemplateMatchTool.EnableMultiScale));
            AddSlider("Min Scale", tool, nameof(TemplateMatchTool.MinScale), 0.5, 1, 0.05);
            AddSlider("Max Scale", tool, nameof(TemplateMatchTool.MaxScale), 1, 2, 0.05);
            AddSlider("Scale Step", tool, nameof(TemplateMatchTool.ScaleStep), 0.01, 0.5, 0.01);
            AddCheckBox("Enable Multi-Angle", tool, nameof(TemplateMatchTool.EnableMultiAngle));
            AddSlider("Angle Step", tool, nameof(TemplateMatchTool.AngleStep), 1, 45, 1);
        }
        #endregion

        #region Feature Match Tool
        private void BuildFeatureMatchToolUI(FeatureMatchTool tool)
        {
            AddEnumComboBox<FeatureDetectorType>("Detector Type", tool, nameof(FeatureMatchTool.DetectorType));
            AddSlider("Max Features", tool, nameof(FeatureMatchTool.MaxFeatures), 100, 2000, 100);
            AddSlider("Ratio Threshold", tool, nameof(FeatureMatchTool.RatioThreshold), 0.5, 1, 0.05);
            AddSlider("Min Match Count", tool, nameof(FeatureMatchTool.MinMatchCount), 4, 50, 1);
            AddCheckBox("Draw Matches", tool, nameof(FeatureMatchTool.DrawMatches));
        }
        #endregion

        #region Blob Tool
        private void BuildBlobToolUI(BlobTool tool)
        {
            AddCheckBox("Use Internal Threshold", tool, nameof(BlobTool.UseInternalThreshold));
            AddSlider("Threshold Value", tool, nameof(BlobTool.ThresholdValue), 0, 255, 1);
            AddCheckBox("Invert Polarity", tool, nameof(BlobTool.InvertPolarity));

            AddSectionHeader("Area Filter");
            AddTextBox("Min Area", tool, nameof(BlobTool.MinArea));
            AddTextBox("Max Area", tool, nameof(BlobTool.MaxArea));

            AddSectionHeader("Shape Filter");
            AddSlider("Min Circularity", tool, nameof(BlobTool.MinCircularity), 0, 1, 0.05);
            AddSlider("Max Circularity", tool, nameof(BlobTool.MaxCircularity), 0, 1, 0.05);
            AddSlider("Max Blob Count", tool, nameof(BlobTool.MaxBlobCount), 1, 100, 1);

            AddSectionHeader("Display");
            AddCheckBox("Draw Contours", tool, nameof(BlobTool.DrawContours));
            AddCheckBox("Draw Bounding Box", tool, nameof(BlobTool.DrawBoundingBox));
            AddCheckBox("Draw Center Point", tool, nameof(BlobTool.DrawCenterPoint));
            AddCheckBox("Draw Labels", tool, nameof(BlobTool.DrawLabels));
        }
        #endregion

        #region Caliper Tool
        private void BuildCaliperToolUI(CaliperTool tool)
        {
            AddSectionHeader("Search Region");
            AddTextBox("Start X", tool, "StartPoint.X");
            AddTextBox("Start Y", tool, "StartPoint.Y");
            AddTextBox("End X", tool, "EndPoint.X");
            AddTextBox("End Y", tool, "EndPoint.Y");
            AddSlider("Search Width", tool, nameof(CaliperTool.SearchWidth), 1, 100, 1);

            AddSectionHeader("Edge Detection");
            AddEnumComboBox<EdgePolarity>("Polarity", tool, nameof(CaliperTool.Polarity));
            AddSlider("Edge Threshold", tool, nameof(CaliperTool.EdgeThreshold), 1, 100, 1);
            AddEnumComboBox<CaliperMode>("Mode", tool, nameof(CaliperTool.Mode));
            AddSlider("Expected Width", tool, nameof(CaliperTool.ExpectedWidth), 1, 500, 1);
        }
        #endregion

        #region Line Fit Tool
        private void BuildLineFitToolUI(LineFitTool tool)
        {
            AddSlider("Num Calipers", tool, nameof(LineFitTool.NumCalipers), 2, 50, 1);
            AddSlider("Search Length", tool, nameof(LineFitTool.SearchLength), 10, 200, 5);
            AddSlider("Search Width", tool, nameof(LineFitTool.SearchWidth), 1, 50, 1);
            AddEnumComboBox<EdgePolarity>("Polarity", tool, nameof(LineFitTool.Polarity));
            AddSlider("Edge Threshold", tool, nameof(LineFitTool.EdgeThreshold), 1, 100, 1);
            AddEnumComboBox<LineFitMethod>("Fit Method", tool, nameof(LineFitTool.FitMethod));
            AddSlider("RANSAC Threshold", tool, nameof(LineFitTool.RansacThreshold), 0.1, 20, 0.5);
            AddSlider("Min Found Calipers", tool, nameof(LineFitTool.MinFoundCalipers), 2, 20, 1);
        }
        #endregion

        #region Circle Fit Tool
        private void BuildCircleFitToolUI(CircleFitTool tool)
        {
            AddTextBox("Center X", tool, "CenterPoint.X");
            AddTextBox("Center Y", tool, "CenterPoint.Y");
            AddSlider("Expected Radius", tool, nameof(CircleFitTool.ExpectedRadius), 10, 500, 5);
            AddSlider("Num Calipers", tool, nameof(CircleFitTool.NumCalipers), 3, 64, 1);
            AddSlider("Search Length", tool, nameof(CircleFitTool.SearchLength), 10, 200, 5);
            AddSlider("Search Width", tool, nameof(CircleFitTool.SearchWidth), 1, 50, 1);
            AddSlider("Start Angle", tool, nameof(CircleFitTool.StartAngle), 0, 360, 5);
            AddSlider("End Angle", tool, nameof(CircleFitTool.EndAngle), 0, 360, 5);
            AddEnumComboBox<EdgePolarity>("Polarity", tool, nameof(CircleFitTool.Polarity));
            AddSlider("Edge Threshold", tool, nameof(CircleFitTool.EdgeThreshold), 1, 100, 1);
            AddEnumComboBox<CircleFitMethod>("Fit Method", tool, nameof(CircleFitTool.FitMethod));
        }
        #endregion

        #region Helper Methods
        private void AddLabel(string text)
        {
            var label = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(Colors.Gray),
                FontSize = 12,
                Margin = new Thickness(0, 5, 0, 5)
            };
            ParametersPanel.Children.Add(label);
        }

        private void AddSectionHeader(string text)
        {
            var header = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD700")),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 10, 0, 5)
            };
            ParametersPanel.Children.Add(header);
        }

        private void AddSlider(string label, object source, string propertyName, double min, double max, double tickFrequency, bool snapToTick = false)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 5) };

            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
            var labelText = new TextBlock
            {
                Text = label + ": ",
                Foreground = new SolidColorBrush(Colors.LightGray),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerPanel.Children.Add(labelText);

            var valueText = new TextBlock
            {
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00BFFF")),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            var valueBinding = new Binding(propertyName)
            {
                Source = source,
                StringFormat = tickFrequency >= 1 ? "F0" : "F2",
                Mode = BindingMode.OneWay
            };
            valueText.SetBinding(TextBlock.TextProperty, valueBinding);
            headerPanel.Children.Add(valueText);

            // 파라미터 도움말 아이콘 추가
            var helpIcon = new HelpIcon
            {
                ToolType = _currentToolType,
                ParameterName = propertyName,
                Margin = new Thickness(5, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            headerPanel.Children.Add(helpIcon);

            panel.Children.Add(headerPanel);

            var slider = new Slider
            {
                Minimum = min,
                Maximum = max,
                TickFrequency = tickFrequency,
                IsSnapToTickEnabled = snapToTick
            };
            var sliderBinding = new Binding(propertyName)
            {
                Source = source,
                Mode = BindingMode.TwoWay
            };
            slider.SetBinding(Slider.ValueProperty, sliderBinding);
            panel.Children.Add(slider);

            ParametersPanel.Children.Add(panel);
        }

        private void AddCheckBox(string label, object source, string propertyName)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };

            var checkBox = new CheckBox
            {
                Content = label,
                Foreground = new SolidColorBrush(Colors.White),
                VerticalAlignment = VerticalAlignment.Center
            };
            var binding = new Binding(propertyName)
            {
                Source = source,
                Mode = BindingMode.TwoWay
            };
            checkBox.SetBinding(CheckBox.IsCheckedProperty, binding);
            panel.Children.Add(checkBox);

            // 파라미터 도움말 아이콘 추가
            var helpIcon = new HelpIcon
            {
                ToolType = _currentToolType,
                ParameterName = propertyName,
                Margin = new Thickness(5, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(helpIcon);

            ParametersPanel.Children.Add(panel);
        }

        private void AddTextBox(string label, object source, string propertyName)
        {
            var labelPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 2) };

            var labelText = new TextBlock
            {
                Text = label,
                Foreground = new SolidColorBrush(Colors.LightGray),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            labelPanel.Children.Add(labelText);

            // 파라미터 도움말 아이콘 추가
            var helpIcon = new HelpIcon
            {
                ToolType = _currentToolType,
                ParameterName = propertyName,
                Margin = new Thickness(5, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            labelPanel.Children.Add(helpIcon);

            ParametersPanel.Children.Add(labelPanel);

            var textBox = new TextBox
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3E3E42")),
                Foreground = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#555555")),
                Padding = new Thickness(5, 3, 5, 3),
                Margin = new Thickness(0, 0, 0, 5)
            };
            var textBinding = new Binding(propertyName)
            {
                Source = source,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
            };
            textBox.SetBinding(TextBox.TextProperty, textBinding);
            ParametersPanel.Children.Add(textBox);
        }

        private void AddEnumComboBox<TEnum>(string label, object source, string propertyName) where TEnum : struct, Enum
        {
            var labelPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 2) };

            var labelText = new TextBlock
            {
                Text = label,
                Foreground = new SolidColorBrush(Colors.LightGray),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            labelPanel.Children.Add(labelText);

            // 파라미터 도움말 아이콘 추가
            var helpIcon = new HelpIcon
            {
                ToolType = _currentToolType,
                ParameterName = propertyName,
                Margin = new Thickness(5, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            labelPanel.Children.Add(helpIcon);

            ParametersPanel.Children.Add(labelPanel);

            var comboBox = new ComboBox
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3E3E42")),
                Foreground = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#555555")),
                Margin = new Thickness(0, 0, 0, 5),
                ItemsSource = Enum.GetValues(typeof(TEnum))
            };
            var binding = new Binding(propertyName)
            {
                Source = source,
                Mode = BindingMode.TwoWay
            };
            comboBox.SetBinding(ComboBox.SelectedItemProperty, binding);
            ParametersPanel.Children.Add(comboBox);
        }
        #endregion
    }
}
