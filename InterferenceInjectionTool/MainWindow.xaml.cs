using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Axes;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace InterferenceInjectionTool
{
    public partial class MainWindow : Window
    {

        private List<SignalData> signalDataList = new List<SignalData>();
        private List<SignalData> signalDataInterList = new List<SignalData>();
        
        private int currentDataRawIndex = 0;
        private int totalRecordsRaw = 0;
        private int currentDataInterIndex = 0;
        private int totalRecordsInter = 0;

        private int currentPageInter = 0;
        private int pageSize = 100;

        private int maxPointsToLoad = 1000;
        private int loadedPoints = 0;

        private List<double> freqMid;

        // PlotModels for the three charts
        public PlotModel RawSignalModel { get; private set; }
        public PlotModel InterferenceSignalModel { get; private set; }
        public PlotModel PreviewSignalModel { get; private set; }

        public MainWindow()
        {
            InitializeComponent();

            // Initialize plot models
            SetupPlotModels();

            // Set DataContext for binding
            DataContext = this;
        }

        private void SetupPlotModels()
        {
            RawSignalModel = new PlotModel();
            RawSignalModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Frequency(MHz)",
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.LightGray,
                IsPanEnabled = false,
                IsZoomEnabled = false
            });
            RawSignalModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Power(dB)",
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.LightGray,
                IsPanEnabled = false,
                IsZoomEnabled = false
            });
            
            InterferenceSignalModel = new PlotModel();
            InterferenceSignalModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Frequency(MHz)",
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.LightGray,
                IsPanEnabled = false,
                IsZoomEnabled = false
            });
            InterferenceSignalModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Power(dB)",
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.LightGray
            });

            PreviewSignalModel = new PlotModel();
            PreviewSignalModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Frequency(MHz)",
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.LightGray
            });
            PreviewSignalModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Power(dB)",
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.LightGray
            });
        }

        private void ImportCSVRawSignal(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                Title = "Select signal data CSV file"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    LoadRawSignalFile(openFileDialog.FileName);
                    RawSignalPathTextBlock.Text = openFileDialog.FileName;
                    statusTextBlock.Text = "File loaded successfully";

                    currentDataRawIndex = 0;
                    UpdateRawSignalChart();
                    UpdateRawSignalPageDisplay();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    statusTextBlock.Text = "Error loading file";
                }
            }
        }


        private void ImportCSVInterferenceSignal(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                Title = "Select signal data CSV file"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    LoadInterferenceSignalFile(openFileDialog.FileName);
                    RawSignalPathTextBlock.Text = openFileDialog.FileName;
                    statusTextBlock.Text = "File loaded successfully";

                    currentDataInterIndex = 0;
                    UpdateInterferenceChart();
                    UpdateInterferencePageDisplay();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    statusTextBlock.Text = "Error loading file";
                }
            }
        }

        private void LoadRawSignalFile(string filePath)
        {
            signalDataList.Clear();

            using (var reader = new StreamReader(filePath))
            {
                string header = reader.ReadLine();

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var values = line.Split('\t');

                    if (values.Length >= 5)
                    {
                        var signalData = new SignalData
                        {
                            SegStartFreq = double.Parse(values[0], CultureInfo.InvariantCulture),
                            CentreFreq = double.Parse(values[1], CultureInfo.InvariantCulture),
                            SegStopFreq = double.Parse(values[2], CultureInfo.InvariantCulture),
                            Count = int.Parse(values[3])
                        };

                        var psdString = values[4].Trim('[', ']');
                        var psdValues = psdString.Split(',').Select(s => double.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToArray();
                        signalData.PsdMeasurements = psdValues;

                        signalDataList.Add(signalData);
                    }
                }
            }

            totalRecordsRaw = signalDataList.Count;
        }

        private void LoadInterferenceSignalFile(string filePath)
        {
            
            double startFreqMHz = 11845.24;
            double centerFreqMHz = 11861.74;
            double stopStepMHz = 11878.24;

            var psdValues = new List<double>();

            foreach (var line in File.ReadLines(filePath))
            {

                var signalData = new SignalData
                {
                    SegStartFreq = startFreqMHz,
                    CentreFreq = centerFreqMHz,
                    SegStopFreq = stopStepMHz
                };

                if (double.TryParse(line.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
                {
                    signalData.PsdMeasurments = value;
                }

                signalDataInterList.Add(signalData);

                totalRecordsInter++;
            }
        }

        private int currentPageRaw = 0;

        private void UpdateRawSignalChart()
        {
            if (signalDataList.Count == 0)
                return;

            int pageCountRaw = (int)Math.Ceiling((double)signalDataList.Count / pageSize);
            currentPageRaw = Math.Max(0, Math.Min(currentPageRaw, pageCountRaw - 1));

            var data = signalDataList[currentDataRawIndex];

            int startIdx = currentPageRaw * pageSize;
            int endIdx = Math.Min(startIdx + pageSize, data.Count);

            totalRecordsRaw = data.PsdMeasurements.Length;

            int visibleCount = endIdx - startIdx;

            double freqStep = (double)(data.SegStopFreq - data.SegStartFreq) / (data.Count - 1);
            RawSignalModel.Series.Clear();
            var rawSeries = new LineSeries { Color = OxyColors.Blue, StrokeThickness = 1.5 };

            for (int i = 0; i < data.PsdMeasurements.Length / 4; i++)
            {
                double freqMHz = (double)data.SegStartFreq + i * (double)freqStep;
                double powerDb = (double)data.PsdMeasurements[i];
                rawSeries.Points.Add(new DataPoint(freqMHz, powerDb));
            }

            RawSignalModel.Series.Add(rawSeries);

            foreach (var model in new[] { RawSignalModel })
            {
                model.Axes.Clear();
                model.Axes.Add(new LinearAxis
                {
                    Position = AxisPosition.Bottom,
                    Title = "Frequency(MHz)",
                    StringFormat = "F2",
                    MajorGridlineStyle = LineStyle.Solid,
                    MajorGridlineColor = OxyColors.LightGray,
                    IsPanEnabled = false,
                    IsZoomEnabled = false
                });
                model.Axes.Add(new LinearAxis
                {
                    Position = AxisPosition.Left,
                    Title = "Power(dB)",
                    StringFormat = "F1",
                    MajorGridlineStyle = LineStyle.Solid,
                    MajorGridlineColor = OxyColors.LightGray,
                    IsPanEnabled = false,
                    IsZoomEnabled = false
                });
                model.InvalidatePlot(true);
            }
        }

        //private void UpdateInterferenceChart()
        //{
        //    if (signalDataInterList.Count == 0)
        //        return;

        //    int pageCountInter = (int)Math.Ceiling((double)maxPointsToLoad / pageSize);
        //    currentPageInter = Math.Max(0, Math.Min(currentPageInter, pageCountInter - 1));

        //    int startIdx = currentPageInter * pageSize;
        //    int endIdx = Math.Min(startIdx + pageSize, maxPointsToLoad);

        //    var data = signalDataInterList;
        //    int visibleCount = endIdx - startIdx;


        //    double freqStep = (double)(data[0].SegStopFreq - data[0].SegStartFreq) / (signalDataInterList.Count - 1);

        //    InterferenceSignalModel.Series.Clear();
        //    var interSeries = new LineSeries { Color = OxyColors.Blue, StrokeThickness = 1.5 };

        //    for (int i = 0; i < visibleCount; i++)
        //    {
        //        if (loadedPoints >= maxPointsToLoad && currentPageInter < 0)
        //            break;

        //        int actualIndex = startIdx + i;
        //        double freqMHz = data[0].SegStartFreq + actualIndex * freqStep;
        //        double powerDb = (double)(data[actualIndex].PsdMeasurments) / (1e11);
        //        interSeries.Points.Add(new DataPoint(freqMHz, powerDb));

        //        loadedPoints++;
        //    }

        //    InterferenceSignalModel.Series.Add(interSeries);

        //    foreach (var model in new[] { InterferenceSignalModel })
        //    {

        //        model.Axes.Clear();
        //        model.Axes.Add(new LinearAxis
        //        {
        //            Position = AxisPosition.Bottom,
        //            Title = "Frequency(MHz)",
        //            StringFormat = "F2",
        //            MajorGridlineStyle = LineStyle.Solid,
        //            MajorGridlineColor = OxyColors.LightGray,
        //            IsPanEnabled = false,
        //            IsZoomEnabled = false
        //        });
        //        model.Axes.Add(new LinearAxis
        //        {
        //            Position = AxisPosition.Left,
        //            Title = "Power(dB)",
        //            StringFormat = "F1",
        //            MajorGridlineStyle = LineStyle.Solid,
        //            MajorGridlineColor = OxyColors.LightGray,
        //            IsPanEnabled = false,
        //            IsZoomEnabled = false
        //        });
        //        model.InvalidatePlot(true);
        //    }
        //}

        // User-defined offset value (in dB)
        private double interfererOffsetDb = -113; // Controlled by the user
        private double centerFreq = 11846.24; //11852.00
        double defaultInterferenceCenter = 11845.24; // Based on original data


        private void UpdateInterferenceChart()
        {
            double rangePower = 100;
            double minPower = interfererOffsetDb - rangePower / 2; 
            double maxPower = interfererOffsetDb + rangePower / 2;            

            if (signalDataInterList.Count == 0)
                return;

            int pageCountInter = (int)Math.Ceiling((double)maxPointsToLoad / pageSize);
            currentPageInter = Math.Max(0, Math.Min(currentPageInter, pageCountInter - 1));

            //int startIdx = currentPageInter * pageSize;
            //int endIdx = Math.Min(startIdx + pageSize, maxPointsToLoad);

            var data = signalDataInterList;

            //double freqStep = (double)(data[0].SegStopFreq - data[0].SegStartFreq) / (signalDataInterList.Count - 1);

            // Calculate the index corresponding to the target frequency
            double freqStep = (double)(signalDataInterList[0].SegStopFreq - signalDataInterList[0].SegStartFreq) / (signalDataInterList.Count - 1);
            int targetIndex = (int)((centerFreq - signalDataInterList[0].SegStartFreq) / freqStep);

            // Clamp the index
            targetIndex = Math.Max(0, Math.Min(targetIndex, signalDataInterList.Count - 1));

            // Center the page around the target index
            int halfPage = pageSize / 2;
            int startIdx = Math.Max(0, targetIndex - halfPage);
            int endIdx = Math.Min(startIdx + pageSize, signalDataInterList.Count);

            // Update `currentPageInter` if you still want pagination tracking
            currentPageInter = targetIndex;

            int visibleCount = endIdx - startIdx;

            InterferenceSignalModel.Series.Clear();
            var interSeries = new LineSeries { Color = OxyColors.Blue, StrokeThickness = 1.5 };

            for (int i = 0; i < visibleCount; i++)
            {
                if (loadedPoints >= maxPointsToLoad && currentPageInter < 0)
                    break;

                int actualIndex = startIdx + i;
                //double freqMHz = data[0].SegStartFreq + actualIndex * freqStep;

                double freqMHz = data[0].SegStartFreq + actualIndex * freqStep;

                double powerDb = ((double)data[actualIndex].PsdMeasurments) / (1e11);

                interSeries.Points.Add(new DataPoint(freqMHz, powerDb));

                loadedPoints++;
            }

            InterferenceSignalModel.Series.Add(interSeries);

            double rangeFreq = 0.05;
            double minFreq = centerFreq - rangeFreq / 2;
            double maxFreq = centerFreq + rangeFreq / 2;

            foreach (var model in new[] { InterferenceSignalModel })
            {
                model.Axes.Clear();

                model.Axes.Add(new LinearAxis
                {
                    Position = AxisPosition.Bottom,
                    Title = "Frequency(MHz)",
                    StringFormat = "F2",
                    MajorGridlineStyle = LineStyle.Solid,
                    MajorGridlineColor = OxyColors.LightGray,
                    IsPanEnabled = false,
                    IsZoomEnabled = false,
                    Minimum = minFreq,
                    Maximum = maxFreq
                });

                // Set fixed Y-axis limits (optional) or let them auto-adjust
                model.Axes.Add(new LinearAxis
                {
                    Position = AxisPosition.Left,
                    Title = "Power(dB)",
                    StringFormat = "F1",
                    MajorGridlineStyle = LineStyle.Solid,
                    MajorGridlineColor = OxyColors.LightGray,
                    IsPanEnabled = false,
                    IsZoomEnabled = false,
                    Minimum = minPower,
                    Maximum = maxPower
                });

                model.InvalidatePlot(true);
            }
        }


        private void UpdateRawSignalPageDisplay()
        {
            pagingRawSignal.Text = $"{currentDataRawIndex + 1}/{totalRecordsRaw}";
        }

        private void UpdateInterferencePageDisplay()
        {
            if (pagingInterference is not null)
            {
                pagingInterference.Text = $"{currentDataInterIndex + 1}/{maxPointsToLoad / pageSize}";
            }
        }

        private void PreviousButtonRawSignal_Click(object sender, RoutedEventArgs e)
        {
            if (currentPageRaw > 0)
            {
                currentPageRaw--;
                currentDataRawIndex--;
                UpdateRawSignalChart();
            }
        }

        private void NextButtonRawSignal_Click(object sender, RoutedEventArgs e)
        {
            int pageCountRaw = (int)Math.Ceiling((double)signalDataList.Count / 1);
            if (currentPageRaw < pageCountRaw - 1)
            {
                currentPageRaw++;
                currentDataRawIndex++;
                UpdateRawSignalChart();
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Export functionality will be implemented here.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void NextButtonInterference_Click(object sender, RoutedEventArgs e)
        {
            int pageCount = (int)Math.Ceiling((double)maxPointsToLoad / pageSize);
            if (currentDataInterIndex < maxPointsToLoad / pageSize - 1)
            {
                currentPageInter++;
                currentDataInterIndex++;
                UpdateInterferenceChart();
                UpdateInterferencePageDisplay();
            }
        }

        private void PreviousButtonInterference_Click(object sender, RoutedEventArgs e)
         {
            if (currentDataInterIndex > 0)
            {
                currentPageInter--;
                currentDataInterIndex--;
                UpdateInterferencePageDisplay();
                UpdateInterferenceChart();
            }
        }

        private void vectorLenghtField_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            int.TryParse(vectorLenghtField.Text, out int result);
            maxPointsToLoad = result;
            loadedPoints = 0;
            currentDataInterIndex = 0;
            currentPageInter = 0;
            UpdateInterferencePageDisplay();
            UpdateInterferenceChart();
        }

        private void offsetField_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            interfererOffsetDb = int.Parse(offsetField.Text);
            UpdateInterferenceChart();
        }
    }

    public class SignalData
    {
        public double SegStartFreq { get; set; }
        public double CentreFreq { get; set; }
        public double SegStopFreq { get; set; }
        public int Count { get; set; }
        public double[] PsdMeasurements { get; set; }
        public double PsdMeasurments { get; set; }
    }
}
