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

        private double spectrumWidthValue = 0;

        private string interferenceFileName;

        private List<double> freqMid;


        private List<double> _frequencies = new List<double>();
        private int _currentTimeStep = 0;
        private List<List<double>> _timeStepsPsdValues = new List<List<double>>();


        public PlotModel RawSignalModel { get; private set; }
        public PlotModel InterferenceSignalModel { get; private set; }
        public PlotModel PreviewSignalModel { get; private set; }

        public MainWindow()
        {
            InitializeComponent();

            SetupPlotModels();

            DataContext = this;
        }

        private void SetupPlotModels()
        {
            RawSignalModel = new PlotModel { PlotMargins = new OxyThickness(60, 40, 20, 40) };

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

                    interferenceFileName = openFileDialog.FileName;

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
            signalDataInterList.Clear();

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
                    signalData.PsdMeasurments = -value;
                }

                signalDataInterList.Add(signalData);

                totalRecordsInter++;
                maxPointsToLoad++;
            }
            int.TryParse(vectorLenghtField.Text, out int result);
            maxPointsToLoad = maxPointsToLoad - result;
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

            double minPower = data.PsdMeasurements.Min();
            double maxPower = data.PsdMeasurements.Max();

            double minFreq = data.SegStartFreq;
            double maxFreq = data.SegStopFreq;

            for (int i = 0; i < data.PsdMeasurements.Length; i += 4)
            {
                double freqMHz = data.SegStartFreq + i * freqStep;
                double powerDb = data.PsdMeasurements[i];
                rawSeries.Points.Add(new DataPoint(freqMHz, powerDb));
            }

            double powerPadding = (maxPower - minPower) * 0.1;
            if (powerPadding == 0) powerPadding = 1;

            double freqPadding = (maxFreq - minFreq) * 0.09;
            if (freqPadding == 0) freqPadding = 1;

            RawSignalModel.Series.Add(rawSeries);

            foreach (var model in new[] { RawSignalModel })
            {
                model.Axes.Clear();
                model.Axes.Add(new LinearAxis
                {
                    Position = AxisPosition.Bottom,
                    Title = "Frequency(MHz)",
                    StringFormat = "F2",
                    MinorStep = 2,
                    MajorStep = 6,
                    Minimum = (double)minFreq - freqPadding,
                    Maximum = (double)maxFreq + freqPadding,
                    MajorGridlineStyle = LineStyle.Solid,
                    MajorGridlineColor = OxyColors.LightGray,

                });
                model.Axes.Add(new LinearAxis
                {
                    Position = AxisPosition.Left,
                    Title = "Power(dB)",
                    StringFormat = "F1",
                    Minimum = minPower - powerPadding,
                    Maximum = maxPower + powerPadding,
                    MajorGridlineStyle = LineStyle.Solid,
                    MajorGridlineColor = OxyColors.LightGray,
                    
                });
                model.InvalidatePlot(true);
            }
        }


        //private void UpdateRawSignalChart()
        //{
        //    if (signalDataList.Count == 0)
        //        return;

        //    int pageCountRaw = (int)Math.Ceiling((double)signalDataList.Count / pageSize);
        //    currentPageRaw = Math.Max(0, Math.Min(currentPageRaw, pageCountRaw - 1));

        //    var data = signalDataList[currentDataRawIndex];

        //    int startIdx = currentPageRaw * pageSize;
        //    int endIdx = Math.Min(startIdx + pageSize, data.Count);

        //    totalRecordsRaw = data.PsdMeasurements.Length;

        //    int visibleCount = endIdx - startIdx;

        //    double freqStep = (double)(data.SegStopFreq - data.SegStartFreq) / (data.Count - 1);
        //    RawSignalModel.Series.Clear();
        //    var rawSeries = new LineSeries { Color = OxyColors.Blue, StrokeThickness = 1.5 };

        //    for (int i = 0; i < data.PsdMeasurements.Length - 200; i++)
        //    {
        //        double freqMHz = (double)data.SegStartFreq + i * (double)freqStep;
        //        double powerDb = (double)data.PsdMeasurements[i];
        //        rawSeries.Points.Add(new DataPoint(freqMHz, powerDb));
        //    }

        //    RawSignalModel.Series.Add(rawSeries);

        //    foreach (var model in new[] { RawSignalModel })
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

        private double interfererOffsetDb = 100;
        private double centerFreq = 11846.24; //11852.00 //end 11878,24
        double defaultInterferenceCenter = 11845.24;


        private void UpdateInterferenceChart()
        {
            double rangePower = 100;
            double minPower = interfererOffsetDb - rangePower / 2;
            double maxPower = interfererOffsetDb + rangePower / 2;

            if (signalDataInterList.Count == 0)
                return;

            int pageCountInter = (int)Math.Ceiling((double)maxPointsToLoad / pageSize);
            currentPageInter = Math.Max(0, Math.Min(currentPageInter, pageCountInter - 1));

            int startIdx = currentPageInter * pageSize;
            int endIdx = Math.Min(startIdx + pageSize, maxPointsToLoad);

            var data = signalDataInterList;

            data[0].SegStopFreq = data[0].SegStartFreq + spectrumWidthValue;

            double freqStep = (double)(data[0].SegStopFreq - data[0].SegStartFreq) / (maxPointsToLoad);

            int visibleCount = endIdx - startIdx;

            double minFreq = data[0].SegStartFreq + startIdx * freqStep;
            double maxFreq = data[0].SegStartFreq + (endIdx - 1) * freqStep;

            InterferenceSignalModel.Series.Clear();
            var interSeries = new LineSeries { Color = OxyColors.Blue, StrokeThickness = 1.5 };

            for (int i = 0; i < visibleCount; i++)
            {
                if (loadedPoints >= maxPointsToLoad && currentPageInter < 0)
                    break;

                int actualIndex = startIdx + i;
                double freqMHz = data[0].SegStartFreq + actualIndex * freqStep;

                double powerDb = ((double)data[actualIndex].PsdMeasurments) / (1e11);

                interSeries.Points.Add(new DataPoint(freqMHz, powerDb));

                loadedPoints++;
            }

            InterferenceSignalModel.Series.Add(interSeries);


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

        private void JumpToFrequency(double targetFrequencyMHz)
        {
            if (signalDataInterList == null || signalDataInterList.Count == 0)
                return;

            double freqStep = (double)(signalDataInterList[0].SegStopFreq - signalDataInterList[0].SegStartFreq) / (signalDataInterList.Count - 1);
            int targetIndex = (int)Math.Round((targetFrequencyMHz - signalDataInterList[0].SegStartFreq) / freqStep);

            if (targetIndex < 0 || targetIndex >= signalDataInterList.Count)
            {
                return;
            }

            currentPageInter = targetIndex / pageSize;

            currentDataInterIndex = currentPageInter;

            UpdateInterferencePageDisplay();

            UpdateInterferenceChart();
        }


        private void UpdateRawSignalPageDisplay()
        {
            pagingRawSignal.Text = $"{currentDataRawIndex + 1}/{signalDataList.Count}";
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
            if (currentDataRawIndex > 0)
            {
                currentPageRaw--;
                currentDataRawIndex--;
                UpdateRawSignalChart();
                UpdateRawSignalPageDisplay();
            }
        }

        private void NextButtonRawSignal_Click(object sender, RoutedEventArgs e)
        {
            int pageCountRaw = (int)Math.Ceiling((double)signalDataList.Count / 1);
            if (currentDataRawIndex < pageCountRaw - 1)
            {
                currentPageRaw++;
                currentDataRawIndex++;
                UpdateRawSignalChart();
                UpdateRawSignalPageDisplay();
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
            if (!int.TryParse(vectorLenghtField.Text, out int result))
                result = 0;

            maxPointsToLoad = result;
            loadedPoints = 0;
            currentDataInterIndex = 0;
            currentPageInter = 0;
            UpdateInterferenceChart();
            UpdateInterferencePageDisplay();
        }

        private void offsetField_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!double.TryParse(offsetField.Text, out double result))
                result = 0;

            interfererOffsetDb = result;
            UpdateInterferenceChart();
        }

        private void TextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (double.TryParse(centerFrequencyField.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double targetFreqMHz))
            {
                JumpToFrequency(targetFreqMHz);
            }
            else if (string.IsNullOrWhiteSpace(centerFrequencyField.Text))
            {
                centerFreq = 0;
                JumpToFrequency(centerFreq);
            }
            else
            {
                MessageBox.Show("Please enter a valid frequency.");
            }
        }

        private void spectrumWidth_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!double.TryParse(spectrumWidth.Text, out double result))
                result = 0;

            spectrumWidthValue = result;

            if (interferenceFileName != null)
            {
                LoadInterferenceSignalFile(interferenceFileName);
            }

            loadedPoints = 0;
            currentDataInterIndex = 0;
            currentPageInter = 0;

            UpdateInterferenceChart();
            UpdateInterferencePageDisplay();
        }



        private void previewButton_Click(object sender, RoutedEventArgs e)
        {
            if (PreviewSignalModel != null)
            {
                PreviewSignalModel.Series.Clear();
            }
            PreviewInjectedSignal("OneToAll");
        }

        private void PreviewInjectedSignal(string strategy)
        {
            if (signalDataList.Count == 0 || signalDataInterList.Count == 0)
                return;

            int cleanRowCount = signalDataList.Count;
            int interferencePageCount = signalDataInterList.Count;

            for (int rowIndex = 0; rowIndex < cleanRowCount; rowIndex++)
            {
                var cleanData = signalDataList[rowIndex];

                // Select interference row
                SignalData interferenceData;
                if (strategy == "RoundRobin")
                {
                    interferenceData = signalDataInterList[rowIndex % interferencePageCount];
                }
                else if (strategy == "OneToAll")
                {
                    interferenceData = signalDataInterList[currentDataInterIndex]; // Use current visible page
                }
                else
                {
                    throw new ArgumentException("Unknown strategy: " + strategy);
                }

                // Setup frequency and power arrays
                int interCount = maxPointsToLoad;
                double interStep = (double)(interferenceData.SegStopFreq - interferenceData.SegStartFreq) / interCount;

                double[] interFreqs = new double[interCount];
                double[] interPowerDb = new double[interCount];

                for (int i = 0; i < interCount; i++)
                {
                    interFreqs[i] = interferenceData.SegStartFreq + i * interStep;
                    interPowerDb[i] = ((double)signalDataInterList[i].PsdMeasurments) / 1e11; // Scale if needed
                }

                // Clean signal setup
                int cleanCount = cleanData.PsdMeasurements.Length;
                double cleanStep = (double)(cleanData.SegStopFreq - cleanData.SegStartFreq) / (cleanCount - 1);

                double[] cleanFreqs = new double[cleanCount];
                for (int i = 0; i < cleanCount; i++)
                    cleanFreqs[i] = cleanData.SegStartFreq + i * cleanStep;

                // Interpolate interference to clean freq points
                double[] interpInterPowerDb = new double[cleanCount];
                for (int i = 0; i < cleanCount; i++)
                    interpInterPowerDb[i] = InterpolateLinear(interFreqs, interPowerDb, cleanFreqs[i]);

                // Combine in mW and convert back to dBm
                double[] combinedDbm = new double[cleanCount];
                for (int i = 0; i < cleanCount; i++)
                {
                    double cleanMw = DbmToMilliwatt(cleanData.PsdMeasurements[i]);
                    double interMw = DbmToMilliwatt(interpInterPowerDb[i]);
                    combinedDbm[i] = 10 * Math.Log10(cleanMw + interMw);
                }

                cleanData.PsdPreview = combinedDbm; // Store preview
            }

            UpdatePreviewChart(); // Show preview for currentDataRawIndex row
        }


        //private void PreviewInjectedSignal()
        //{
        //    if (signalDataList.Count == 0 || signalDataInterList.Count == 0)
        //        return;

        //    var cleanData = signalDataList[currentDataRawIndex];
        //    var interferenceData = signalDataInterList[0];

        //    int cleanCount = cleanData.PsdMeasurements.Length;

        //    double freqStep = (double)(cleanData.SegStopFreq - cleanData.SegStartFreq) / (cleanCount - 1);

        //    // Step 1: Build clean frequency axis
        //    double[] cleanFreqs = new double[cleanCount];
        //    for (int i = 0; i < cleanCount; i++)
        //    {
        //        cleanFreqs[i] = cleanData.SegStartFreq + i * freqStep;
        //    }

        //    // Step 2: Interference frequency and power values
        //    int interCount = maxPointsToLoad;
        //    double interStep = (double)(interferenceData.SegStopFreq - interferenceData.SegStartFreq) / interCount;

        //    double[] interFreqs = new double[interCount];
        //    double[] interPowerDb = new double[interCount];

        //    for (int i = 0; i < interCount; i++)
        //    {
        //        interFreqs[i] = interferenceData.SegStartFreq + i * interStep;
        //        interPowerDb[i] = ((double)signalDataInterList[i].PsdMeasurments) / (1e11);  // You may adjust scaling
        //    }

        //    // Step 3: Interpolate interference signal to clean frequency points
        //    double[] interpInterPowerDb = new double[cleanCount];
        //    for (int i = 0; i < cleanCount; i++)
        //    {
        //        interpInterPowerDb[i] = InterpolateLinear(interFreqs, interPowerDb, cleanFreqs[i]);
        //    }

        //    // Step 4: Combine signals in milliwatts
        //    double[] combinedMilliwatts = new double[cleanCount];
        //    for (int i = 0; i < cleanCount; i++)
        //    {
        //        double cleanMw = DbmToMilliwatt(cleanData.PsdMeasurements[i]);
        //        double interMw = DbmToMilliwatt(interpInterPowerDb[i]);
        //        combinedMilliwatts[i] = cleanMw + interMw;
        //    }

        //    // Step 5: Convert back to dBm
        //    double[] combinedDbm = combinedMilliwatts.Select(mw => 10 * Math.Log10(mw)).ToArray();

        //    // Step 6: Visualize on RawSignalModel
        //    var previewSeries = new LineSeries { Color = OxyColors.Red, StrokeThickness = 1.5 };

        //    for (int i = 0; i < combinedDbm.Length; i++)  // Downsample for speed
        //    {
        //        double freq = cleanFreqs[i];
        //        double powerDb = combinedDbm[i];
        //        previewSeries.Points.Add(new DataPoint(freq, powerDb));
        //    }

        //    PreviewSignalModel.Series.Add(previewSeries);
        //    PreviewSignalModel.InvalidatePlot(true);
        //}



        private void UpdatePreviewChart()
        {
            if (signalDataList.Count == 0)
                return;

            var data = signalDataList[currentDataRawIndex];
            if (data.PsdPreview == null || data.PsdPreview.Length == 0)
                return;

            int dataCount = data.PsdPreview.Length;
            double freqStep = (double)(data.SegStopFreq - data.SegStartFreq) / (dataCount - 1);

            // Create series for combined preview signal
            var combinedSeries = new LineSeries
            {
                Color = OxyColors.OrangeRed,
                StrokeThickness = 1.5,
                Title = "Combined Signal (Clean + Interference)"
            };

            for (int i = 0; i < dataCount; i += 4)
            {
                double freqMHz = data.SegStartFreq + i * freqStep;
                double powerDb = data.PsdPreview[i];
                combinedSeries.Points.Add(new DataPoint(freqMHz, powerDb));
            }

            // Clear previous series and axes
            PreviewSignalModel.Series.Clear();
            PreviewSignalModel.Series.Add(combinedSeries);

            double minFreq = data.SegStartFreq;
            double maxFreq = data.SegStopFreq;

            double minPower = signalDataList[currentDataRawIndex].PsdMeasurements.Min();
            double maxPower = signalDataList[currentDataRawIndex].PsdMeasurements.Max();

            double powerPadding = (maxPower - minPower) * 0.1;
            if (powerPadding == 0) powerPadding = 1;

            double freqPadding = (maxFreq - minFreq) * 0.09;
            if (freqPadding == 0) freqPadding = 1;

            // Configure axes
            PreviewSignalModel.Axes.Clear();
            PreviewSignalModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Frequency(MHz)",
                StringFormat = "F2",
                MinorStep = 2,
                MajorStep = 6,
                Minimum = minFreq - freqPadding,
                Maximum = maxFreq + freqPadding,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.LightGray
            });

            PreviewSignalModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Power(dB)",
                StringFormat = "F1",
                Minimum = minPower - powerPadding,
                Maximum = maxPower + powerPadding,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.LightGray
            });

            // Redraw
            PreviewSignalModel.InvalidatePlot(true);
        }







        private double DbmToMilliwatt(double dbm)
        {
            return Math.Pow(10, dbm / 10.0);
        }

        private double InterpolateLinear(double[] x, double[] y, double xi)
        {
            if (xi <= x[0]) return y[0];
            if (xi >= x[^1]) return y[^1];

            for (int i = 0; i < x.Length - 1; i++)
            {
                if (x[i] <= xi && xi <= x[i + 1])
                {
                    double t = (xi - x[i]) / (x[i + 1] - x[i]);
                    return y[i] + t * (y[i + 1] - y[i]);
                }
            }

            return y[^1]; // fallback
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
        public double[] PsdPreview { get; set; }
    }
}
