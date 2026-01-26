using BODA_VISION_AI.Models;
using OpenCvSharp;
using System;
using System.Diagnostics;

namespace BODA_VISION_AI.VisionTools.ImageProcessing
{
    /// <summary>
    /// Grayscale 변환 도구 (Cognex VisionPro CogImageConvertTool 대체)
    /// </summary>
    public class GrayscaleTool : VisionToolBase
    {
        public GrayscaleTool()
        {
            Name = "Grayscale";
            ToolType = "GrayscaleTool";
        }

        public override VisionResult Execute(Mat inputImage)
        {
            var result = new VisionResult();
            var sw = Stopwatch.StartNew();

            try
            {
                Mat workImage = GetROIImage(inputImage);
                Mat outputImage = new Mat();

                // 이미 Grayscale인지 확인
                if (workImage.Channels() == 1)
                {
                    outputImage = workImage.Clone();
                }
                else
                {
                    Cv2.CvtColor(workImage, outputImage, ColorConversionCodes.BGR2GRAY);
                }

                result.Success = true;
                result.Message = "Grayscale 변환 완료";
                result.OutputImage = outputImage;
                result.Data["Channels"] = outputImage.Channels();
                result.Data["Width"] = outputImage.Width;
                result.Data["Height"] = outputImage.Height;

                if (workImage != inputImage)
                    workImage.Dispose();
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Grayscale 변환 실패: {ex.Message}";
            }

            sw.Stop();
            ExecutionTime = sw.Elapsed.TotalMilliseconds;
            LastResult = result;
            return result;
        }

        public override VisionToolBase Clone()
        {
            return new GrayscaleTool
            {
                Name = this.Name,
                ToolType = this.ToolType,
                IsEnabled = this.IsEnabled,
                ROI = this.ROI,
                UseROI = this.UseROI
            };
        }
    }
}
