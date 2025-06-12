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
using System.Text;
using OxyPlot.Annotations;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Controls;

namespace InterferenceInjectionTool
{
    public partial class MainWindow : Window
    {

        private List<SignalData> signalDataList = new List<SignalData>();
        private List<SignalData> signalDataInterList = new List<SignalData>();

        private List<SignalData> signalDataInterListWithOffset = new List<SignalData>();

        private int currentDataRawIndex = 0;
        private int totalRecordsRaw = 0;
        private int currentDataInterIndex = 0;
        private int totalRecordsInter = 0;

        private int currentPageInter = 0;
        private int pageSize = 400;

        private int maxPointsToLoad = 0;
        private int loadedPoints = 0;

        private double interfererOffsetDb = 100;
        private double spectrumWidthValue = 0;

        private string interferenceFileName;

        public PlotModel RawSignalModel { get; private set; }
        public PlotModel InterferenceSignalModel { get; private set; }
        public PlotModel PreviewSignalModel { get; private set; }

        public PlotModel ExportedSignalModel { get; private set; }

        public MainWindow()
        {
            InitializeComponent();

            SetupPlotModels();

            SetUpExportedSignalModel();

            DataContext = this;
        }

        private void SetUpExportedSignalModel()
        {
            ExportedSignalModel = new PlotModel();

            ExportedSignalModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Frequency(MHz)",
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.LightGray,
                IsPanEnabled = false,
                IsZoomEnabled = false
            });
            ExportedSignalModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Power(dB)",
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.LightGray,
                IsPanEnabled = false,
                IsZoomEnabled = false
            });
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
                    InterferencePathTextBlock.Text = openFileDialog.FileName;
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

            maxPointsToLoad = int.Parse(vectorLenghtField.Text);

            foreach (var line in File.ReadLines(filePath))
            {
                var signalData = new SignalData
                {
                    SegStartFreq = startFreqMHz,
                    CentreFreq = centerFreqMHz,
                    SegStopFreq = stopStepMHz
                };

                if (maxPointsToLoad > totalRecordsInter)
                {
                    if (double.TryParse(line, NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
                    {
                        double valueInDbm = 10 * Math.Log10(value);
                        signalData.PsdMeasurments = valueInDbm;
                    }

                    signalDataInterList.Add(signalData);
                    signalDataInterListWithOffset.Add(signalData);
                    totalRecordsInter++;
                }
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

            double minPower = data.PsdMeasurements.Min();
            double maxPower = data.PsdMeasurements.Max();

            double minFreq = data.SegStartFreq;
            double maxFreq = data.SegStopFreq;

            for (int i = 0; i < data.PsdMeasurements.Length; i++)
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

        private void UpdateInterferenceChart()
        {
            if (signalDataInterList.Count == 0)
                return;

            int pageCountInter = (int)Math.Ceiling((double)(maxPointsToLoad / pageSize));
            currentPageInter = Math.Max(0, Math.Min(currentPageInter, pageCountInter - 1));

            int startIdx = currentPageInter * pageSize;
            int endIdx = Math.Min(startIdx + pageSize, totalRecordsInter);

            List<SignalData> data;

            if (signalDataInterList[0].PsdMeasurments != signalDataInterListWithOffset[0].PsdMeasurments)
            {
                data = signalDataInterListWithOffset;
                signalDataInterListWithOffset[0].SegStartFreq = signalDataList[0].SegStartFreq;
                signalDataInterListWithOffset[0].SegStopFreq = signalDataList[0].SegStopFreq;
            }
            else
            {
                data = signalDataInterList;
                signalDataInterListWithOffset[0].SegStartFreq = signalDataList[0].SegStartFreq;
                signalDataInterListWithOffset[0].SegStopFreq = signalDataList[0].SegStopFreq;
            }

            double freqStep = (double)(data[0].SegStopFreq - data[0].SegStartFreq) / maxPointsToLoad;

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

                double powerDb = ((double)data[actualIndex].PsdMeasurments);

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
                    //IsPanEnabled = false,
                    //IsZoomEnabled = false,
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
                    //IsPanEnabled = false,
                    //IsZoomEnabled = false,
                    //Minimum = minPower,
                    //Maximum = maxPower
                });

                model.InvalidatePlot(true);
            }
        }

        private void JumpToFrequency(double targetFrequencyMHz)
        {
            if (signalDataInterList == null || signalDataInterList.Count == 0)
                return;

            signalDataInterList[0].SegStopFreq = signalDataInterList[0].SegStartFreq + spectrumWidthValue;

            double freqStep = (double)(signalDataInterList[0].SegStopFreq - signalDataInterList[0].SegStartFreq) / (spectrumWidthValue * 1000);
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
                pagingInterference.Text = $"{currentDataInterIndex + 1}/{(maxPointsToLoad / pageSize)}";
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
            int pageCount = (int)Math.Ceiling((double)(maxPointsToLoad / pageSize));
            if (currentDataInterIndex < (maxPointsToLoad / pageSize) - 1)
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

            maxPointsToLoad = 0;
            totalRecordsInter = 0;
            loadedPoints = 0;
            currentDataInterIndex = 0;
            currentPageInter = 0;
            if (interferenceFileName != null)
            {
                LoadInterferenceSignalFile(interferenceFileName);
            }
            UpdateInterferenceChart();
            UpdateInterferencePageDisplay();
        }

        private void offsetField_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!double.TryParse(offsetField.Text, out double result))
                result = 0;

            interfererOffsetDb = result;

            signalDataInterListWithOffset.Clear();
            signalDataInterListWithOffset = signalDataInterList.Select(item => item.Clone()).ToList();

            foreach (var power in signalDataInterListWithOffset)
            {
                power.PsdMeasurments = power.PsdMeasurments + result;
            }

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
                //centerFreq = 0;
                //JumpToFrequency(centerFreq);
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
                maxPointsToLoad = 0;
                LoadInterferenceSignalFile(interferenceFileName);
            }

            loadedPoints = 0;
            currentDataInterIndex = 0;
            currentPageInter = 0;

            UpdateInterferenceChart();
            UpdateInterferencePageDisplay();
        }

        private void btnOneToAll_Checked(object sender, RoutedEventArgs e)
        {
            strategy = "OneToAll";
        }

        private void btnRoundRobin_Checked(object sender, RoutedEventArgs e)
        {
            strategy = "RoundRobin";
        }

        private string strategy;

        private void previewButton_Click(object sender, RoutedEventArgs e)
        {
            if (PreviewSignalModel != null)
            {
                PreviewSignalModel.Series.Clear();
            }
            PreviewInjectedSignal(strategy);
        }

        private void PreviewInjectedSignal(string strategy)
        {
            if (signalDataList.Count == 0 || signalDataInterList.Count == 0)
                return;

            int cleanRowCount = signalDataList.Count;
            int interferencePageCount = signalDataInterListWithOffset.Count;

            int timer = 0;

            for (int rowIndex = 0; rowIndex < cleanRowCount; rowIndex++)
            {
                var cleanData = signalDataList[rowIndex];
                List<SignalData> interferenceData = new List<SignalData>();

                if (strategy == "RoundRobin")
                {
                    UpdateInterferenceChart();

                    if ((maxPointsToLoad / pageSize) < signalDataList.Count && currentDataInterIndex == (maxPointsToLoad / pageSize))
                    {
                        currentDataInterIndex = 0;
                    }

                    interferenceData = signalDataInterListWithOffset.Take(new Range(new Index(currentDataInterIndex * 401, false), new Index((currentDataInterIndex * 401) + 401, false))).ToList();

                    currentDataInterIndex++;

                    if (interferenceData.Count == 0)
                    {
                        continue;
                    }

                    var lineSeries = InterferenceSignalModel.Series.OfType<LineSeries>().FirstOrDefault();
                    if (lineSeries != null)
                    {
                        var first = lineSeries.Points[0];
                        var last = lineSeries.Points[^1];

                        signalDataInterListWithOffset[0].SegStartFreq = first.X;
                        signalDataInterListWithOffset[0].SegStopFreq = last.X;
                    }
                }
                else if (strategy == "OneToAll")
                {
                    UpdateInterferenceChart();

                    interferenceData = signalDataInterListWithOffset.Take(new Range(new Index(currentDataInterIndex * 401, false), new Index((currentDataInterIndex * 401) + 401, false))).ToList();

                    var lineSeries = InterferenceSignalModel.Series.OfType<LineSeries>().FirstOrDefault();
                    if (lineSeries != null)
                    {
                        var first = lineSeries.Points[0];
                        var last = lineSeries.Points[^1];

                        signalDataInterListWithOffset[0].SegStartFreq = first.X;
                        signalDataInterListWithOffset[0].SegStopFreq = last.X;
                    }
                }
                else
                {
                    throw new ArgumentException("Unknown strategy: " + strategy);
                }

                int interCount = interferenceData.Count;
                double interStep = (interferenceData[0].SegStopFreq - interferenceData[0].SegStartFreq) / (interCount - 1);

                double[] interFreqs = new double[interCount];
                double[] interPowerDb = new double[interCount];

                for (int i = 0; i < interCount; i++)
                {
                    interFreqs[i] = interferenceData[0].SegStartFreq + i * interStep;
                    interPowerDb[i] = interferenceData[i].PsdMeasurments;
                }

                int cleanCount = cleanData.PsdMeasurements.Length;
                double cleanStep = (cleanData.SegStopFreq - cleanData.SegStartFreq) / (cleanCount - 1);

                double[] cleanFreqs = new double[cleanCount];
                for (int i = 0; i < cleanCount; i++)
                    cleanFreqs[i] = cleanData.SegStartFreq + i * cleanStep;

                double[] combinedDbm = new double[cleanCount];

                for (int i = 0; i < cleanCount; i++)
                {
                    double cleanFreq = cleanFreqs[i];
                    double cleanPowerDbm = cleanData.PsdMeasurements[i];
                    double combinedPowerDbm = cleanPowerDbm;

                    if (cleanFreq >= signalDataInterListWithOffset[0].SegStartFreq && cleanFreq <= signalDataInterListWithOffset[0].SegStopFreq)
                    {
                        double interPowerDbm = InterpolateLinear(interFreqs, interPowerDb, cleanFreq);
                        double cleanMw = DbmToMilliwatt(cleanPowerDbm);
                        double interMw = DbmToMilliwatt(interPowerDbm);
                        combinedPowerDbm = 10 * Math.Log10(cleanMw + interMw);
                    }

                    combinedDbm[i] = combinedPowerDbm;
                }

                cleanData.PsdPreview = combinedDbm;
            }

            UpdatePreviewChart();
        }

        private void UpdatePreviewChart()
        {
            if (signalDataList.Count == 0)
                return;

            var data = signalDataList[currentDataRawIndex];
            if (data.PsdPreview == null || data.PsdPreview.Length == 0)
                return;

            int dataCount = data.PsdPreview.Length;
            double freqStep = (double)(data.SegStopFreq - data.SegStartFreq) / (dataCount - 1);

            var rawSeries = new LineSeries
            {
                Color = OxyColors.Blue,
                StrokeThickness = 1.0,
                Title = "Clean Signal"
            };

            for (int i = 0; i < dataCount; i++)
            {
                double freqMHz = data.SegStartFreq + i * freqStep;
                double powerDb = data.PsdPreview[i];
                rawSeries.Points.Add(new DataPoint(freqMHz, powerDb));
            }

            PreviewSignalModel.Series.Clear();
            PreviewSignalModel.Annotations.Clear();

            PreviewSignalModel.Series.Add(rawSeries);

            double minFreq = data.SegStartFreq;
            double maxFreq = data.SegStopFreq;

            double minPower = data.PsdPreview.Min();
            double maxPower = data.PsdPreview.Max();

            double powerPadding = (maxPower - minPower) * 0.1;
            if (powerPadding == 0) powerPadding = 1;

            double freqPadding = (maxFreq - minFreq) * 0.09;
            if (freqPadding == 0) freqPadding = 1;

            PreviewSignalModel.Axes.Clear();
            PreviewSignalModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Frequency (MHz)",
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
                Title = "Power (dB)",
                StringFormat = "F1",
                Minimum = minPower - powerPadding,
                Maximum = maxPower + powerPadding,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.LightGray
            });

            PreviewSignalModel.InvalidatePlot(true);

            UpdateInterferenceChart();
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

            return y[^1];
        }

        private void btnExportSignalWithInterference_Click(object sender, RoutedEventArgs e)
        {
            ExportInterferedSignalToCsv();
        }

        private void ExportInterferedSignalToCsv()
        {
            if (signalDataList.Count == 0)
            {
                MessageBox.Show("No data to export.");
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "CSV files (*.csv)|*.csv";
            saveFileDialog.Title = "Export Interfered Signal";
            saveFileDialog.FileName = "interfered_signal.csv";

            if (saveFileDialog.ShowDialog() != true)
            {
                return;
            }

            StringBuilder csv = new StringBuilder();

            csv.AppendLine("SEG_START_FREQ      CENTRE_FREQ      SEG_STOP_FREQ    COUNT PSD_MEAS");

            foreach (var data in signalDataList)
            {
                if (data.PsdPreview == null || data.PsdPreview.Length == 0)
                    continue;

                int count = data.PsdPreview.Length;
                double freqStep = (double)(data.SegStopFreq - data.SegStartFreq) / (count - 1);

                csv.Append($"{data.SegStartFreq.ToString(CultureInfo.InvariantCulture)}\t" +
                           $"{data.CentreFreq.ToString(CultureInfo.InvariantCulture)}\t" +
                           $"{data.SegStopFreq.ToString(CultureInfo.InvariantCulture)}\t" +
                           $"{data.PsdPreview.Length}\t[");

                for (int i = 0; i < count; i++)
                {
                    double powerDb = data.PsdPreview[i];
                    csv.Append(powerDb.ToString("F2", CultureInfo.InvariantCulture));

                    if (i != count - 1)
                        csv.Append(", ");
                }

                csv.AppendLine("]");
            }

            try
            {
                File.WriteAllText(saveFileDialog.FileName, csv.ToString());
                MessageBox.Show("Export completed successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to export: " + ex.Message);
            }
        }

        private void btnAnalyseExport_Click(object sender, RoutedEventArgs e)
        {
            MainTabControl.SelectedItem = TabAnalyseExport;
            btnAnalyseExport.Background = Brushes.LightGray;
            btnAddInterference.Background = Brushes.Transparent;
        }

        private void btnAddInterference_Click(object sender, RoutedEventArgs e)
        {
            MainTabControl.SelectedItem = TabAddInterference;
            btnAnalyseExport.Background = Brushes.Transparent;
            btnAddInterference.Background = Brushes.LightGray;
        }


        /*-----------------------------------------------------------------------------------------------------------------------------------------------------------------------*/
        //Export & Analyse Tab

        private string exportedSignalFileName;

        private int currentDataExportIndex = 0;
        private int totalRecordsExport = 0;

        private int currentPageExport = 0;

        private List<SignalData> signalDataExportList = new List<SignalData>();

        private void ImportCSVExportedSignal(object sender, RoutedEventArgs e)
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
                    LoadExportedSignalFile(openFileDialog.FileName);
                    ExportedSignalFilePathText.Text = openFileDialog.FileName;

                    exportedSignalFileName = openFileDialog.FileName;

                    currentDataExportIndex = 0;
                    UpdateExportedSignalChart();
                    UpdateExportedSignalPageDisplay();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    statusTextBlock.Text = "Error loading file";
                }
            }
        }

        private void LoadExportedSignalFile(string filePath)
        {
            signalDataExportList.Clear();

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

                        signalDataExportList.Add(signalData);
                    }
                }
            }

            totalRecordsExport = signalDataExportList.Count;

            ComputeStatistics();
        }

        private void UpdateExportedSignalChart()
        {
            if (signalDataExportList.Count == 0)
                return;

            var data = signalDataExportList[currentDataExportIndex];
            double freqStep = (data.SegStopFreq - data.SegStartFreq) / (data.Count - 1);
            int count = data.PsdMeasurements.Length;

            ExportedSignalModel.Series.Clear();

            var exportedSeries = new LineSeries { Title = "raw signal", Color = OxyColors.SteelBlue, StrokeThickness = 1.5 };
            for (int i = 0; i < count; i++)
            {
                double freqMHz = data.SegStartFreq + i * freqStep;
                exportedSeries.Points.Add(new DataPoint(freqMHz, data.PsdMeasurements[i]));
            }
            ExportedSignalModel.Series.Add(exportedSeries);

            if (ShowMaxCheckbox.IsChecked == true)
            {
                var maxSeries = new LineSeries { Title = "max", Color = OxyColors.Red, StrokeThickness = 1.5 };
                for (int i = 0; i < count; i++)
                {
                    double freqMHz = data.SegStartFreq + i * freqStep;
                    maxSeries.Points.Add(new DataPoint(freqMHz, maxValues[i]));
                }
                ExportedSignalModel.Series.Add(maxSeries);
            }

            if (ShowMinCheckbox.IsChecked == true)
            {
                var minSeries = new LineSeries { Title = "min", Color = OxyColors.Green, StrokeThickness = 1.5 };
                for (int i = 0; i < count; i++)
                {
                    double freqMHz = data.SegStartFreq + i * freqStep;
                    minSeries.Points.Add(new DataPoint(freqMHz, minValues[i]));
                }
                ExportedSignalModel.Series.Add(minSeries);
            }

            if (ShowAverageCheckbox.IsChecked == true)
            {
                var avgSeries = new LineSeries { Title = "average", Color = OxyColors.Orange, StrokeThickness = 1.5 };
                for (int i = 0; i < count; i++)
                {
                    double freqMHz = data.SegStartFreq + i * freqStep;
                    avgSeries.Points.Add(new DataPoint(freqMHz, meanValues[i]));
                }
                ExportedSignalModel.Series.Add(avgSeries);
            }

            ExportedSignalModel.Axes.Clear();
            ExportedSignalModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Frequency(MHz)",
                StringFormat = "F2",
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.LightGray,
                Minimum = data.SegStartFreq,
                Maximum = data.SegStopFreq
            });

            ExportedSignalModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Power(dB)",
                StringFormat = "F1",
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.LightGray
            });

            ExportedSignalModel.InvalidatePlot(true);
        }


        private void PreviousButtonExported_Click(object sender, RoutedEventArgs e)
        {
            if (currentDataExportIndex > 0)
            {
                currentPageExport--;
                currentDataExportIndex--;
                ComputeStatistics();
                UpdateExportedSignalChart();
                UpdateExportedSignalPageDisplay();
            }
        }

        private void NextButtonExported_Click(object sender, RoutedEventArgs e)
        {
            int pageCountExported = (int)Math.Ceiling((double)signalDataExportList.Count / 1);
            if (currentDataExportIndex < pageCountExported - 1)
            {
                currentPageExport++;
                currentDataExportIndex++;
                ComputeStatistics();
                UpdateExportedSignalChart();
                UpdateExportedSignalPageDisplay();
            }
        }

        private void UpdateExportedSignalPageDisplay()
        {
            pagingExportedSignal.Text = $"{currentDataExportIndex + 1}/{signalDataExportList.Count}";
        }

        private double[] meanValues;
        private double[] minValues;
        private double[] maxValues;

        private void ComputeStatistics()
        {
            if (signalDataExportList.Count == 0)
                return;


            if (meanValues != null)
            {
                Array.Clear(meanValues);
                Array.Clear(minValues);
                Array.Clear(maxValues);
            }

            int count = signalDataExportList[0].PsdMeasurements.Length;
            meanValues = new double[count];
            maxValues = new double[count];
            minValues = new double[count];

            for (int i = 0; i < count; i++)
            {
                var valuesAtFreq = signalDataExportList.Select(s => s.PsdMeasurements[i]).ToArray();
                meanValues[i] = valuesAtFreq.Average();
                maxValues[i] = valuesAtFreq.Max();
                minValues[i] = valuesAtFreq.Min();
            }
        }

        private void StatisticsCheckboxChanged(object sender, RoutedEventArgs e)
        {
            UpdateExportedSignalChart();
        }

        private void ExportStatistics_Click(object sender, RoutedEventArgs e)
        {
            if (meanValues == null || minValues == null || maxValues == null || signalDataExportList.Count == 0)
            {
                MessageBox.Show("No statistics to export. Please load data and compute statistics first.", "No Data", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                Title = "Save Statistics CSV File",
                FileName = "exported_statistics.csv"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    using (var writer = new StreamWriter(saveFileDialog.FileName))
                    {
                        writer.WriteLine("Frequency(MHz),Mean,Max,Min");

                        var current = signalDataExportList[currentDataExportIndex];
                        double freqStep = (current.SegStopFreq - current.SegStartFreq) / (current.Count - 1);

                        for (int i = 0; i < current.Count; i++)
                        {
                            double freq = current.SegStartFreq + i * freqStep;
                            writer.WriteLine($"{freq.ToString("F6", CultureInfo.InvariantCulture)},{meanValues[i].ToString("F6", CultureInfo.InvariantCulture)},{maxValues[i].ToString("F6", CultureInfo.InvariantCulture)},{minValues[i].ToString("F6", CultureInfo.InvariantCulture)}");
                        }
                    }

                    MessageBox.Show("Statistics exported successfully.", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error exporting statistics: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
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

        public SignalData Clone()
        {
            return new SignalData 
            { 
                SegStartFreq = this.SegStartFreq,
                CentreFreq = this.CentreFreq,
                SegStopFreq = this.SegStopFreq,
                Count = this.Count,
                PsdMeasurements = this.PsdMeasurements,
                PsdMeasurments = this.PsdMeasurments,
                PsdPreview = this.PsdPreview
            };
        }
    }
}
