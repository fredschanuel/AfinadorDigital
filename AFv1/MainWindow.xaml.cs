using NAudio.Wave;
using MathNet.Numerics.IntegralTransforms;
using MathNet.Numerics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AFv1
{
    public partial class MainWindow : System.Windows.Window
    {
        private WaveInEvent waveIn;
        private AudioFileReader audioFileReader;
        private string targetNote;
        private double targetFrequency;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void StartRecording()
        {
            try
            {
                waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(44100, 1)
                };
                waveIn.DataAvailable += OnDataAvailable;
                waveIn.StartRecording();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao iniciar a gravação: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            var samples = new float[e.BytesRecorded / 2];
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = BitConverter.ToInt16(e.Buffer, i * 2) / 32768f;
            }

            var fftSamples = new MathNet.Numerics.Complex32[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                fftSamples[i] = new MathNet.Numerics.Complex32(samples[i], 0);
            }

            Fourier.Forward(fftSamples, FourierOptions.Matlab);

            double maxMagnitude = 0;
            int maxIndex = 0;
            for (int i = 0; i < fftSamples.Length / 2; i++)
            {
                double magnitude = fftSamples[i].Magnitude;
                if (magnitude > maxMagnitude)
                {
                    maxMagnitude = magnitude;
                    maxIndex = i;
                }
            }

            double frequency = maxIndex * 44100.0 / fftSamples.Length;
            UpdateUI(frequency);
        }

        private void UpdateUI(double currentFrequency)
        {
            if (currentFrequency < 50 || currentFrequency > 400)
            {
                Dispatcher.Invoke(() => frequencyLabel.Content = "Frequência fora do intervalo.");
                return;
            }

            double difference = Math.Abs(currentFrequency - targetFrequency);
            double percentage = Math.Max(0, 100 - (difference / targetFrequency * 100));

            Dispatcher.Invoke(() =>
            {
                frequencyLabel.Content = $"Frequência: {currentFrequency:F2} Hz";
                tuningProgressBar.Value = percentage;

                if (difference < 1.0)
                {
                    noteLabel.Content = $"{targetNote} (Afinado!)";
                    noteLabel.Foreground = new SolidColorBrush(Colors.Green);
                }
                else
                {
                    noteLabel.Content = $"{targetNote} ({currentFrequency:F2} Hz)";
                    noteLabel.Foreground = new SolidColorBrush(Colors.Red);
                }
            });
        }

        private void NoteButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button != null)
            {
                targetNote = button.Content.ToString();
                targetFrequency = GetFrequencyFromNote(targetNote);

                Dispatcher.Invoke(() =>
                {
                    noteLabel.Content = $"Nota Alvo: {targetNote} ({targetFrequency:F2} Hz)";
                    noteLabel.Foreground = new SolidColorBrush(Colors.Black);
                });
            }
        }

        private double GetFrequencyFromNote(string note)
        {
            var noteFrequencies = new Dictionary<string, double>
            {
                { "E", 82.41 },
                { "A", 110.00 },
                { "D", 146.83 },
                { "G", 196.00 },
                { "B", 246.94 },
                { "e", 329.63 }
            };

            return noteFrequencies.ContainsKey(note) ? noteFrequencies[note] : 0.0;
        }

        private void StopRecording()
        {
            waveIn?.StopRecording();
            waveIn?.Dispose();
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            StartRecording();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StopRecording();
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Este é um afinador digital para guitarra. Use os botões para selecionar a nota alvo e toque a corda correspondente no instrumento para verificar se está afinado.",
                            "Ajuda", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LoadFileButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Arquivos WAV (*.wav)|*.wav"
            };
                
            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                ProcessAudioFile(filePath);
            }
        }

        private void ProcessAudioFile(string filePath)
        {
            try
            {
                audioFileReader = new AudioFileReader(filePath);
                var sampleProvider = audioFileReader.ToSampleProvider();
                var buffer = new float[audioFileReader.WaveFormat.SampleRate];
                int samplesRead;

                while ((samplesRead = sampleProvider.Read(buffer, 0, buffer.Length)) > 0)
                {
                    var fftSamples = buffer.Take(samplesRead).ToArray();
                    ApplyFFTAndUpdateUI(fftSamples, audioFileReader.WaveFormat.SampleRate);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao processar o arquivo de áudio: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFFTAndUpdateUI(float[] samples, int sampleRate)
        {
            int fftSize = 16384;

            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] *= (float)(0.54 - 0.46 * Math.Cos(2 * Math.PI * i / (samples.Length - 1)));
            }

            var paddedSamples = new MathNet.Numerics.Complex32[fftSize];
            for (int i = 0; i < samples.Length && i < fftSize; i++)
            {
                paddedSamples[i] = new MathNet.Numerics.Complex32(samples[i], 0);
            }

            Fourier.Forward(paddedSamples, FourierOptions.Matlab);

            double[] magnitudes = new double[fftSize / 2];
            for (int i = 0; i < fftSize / 2; i++)
            {
                magnitudes[i] = paddedSamples[i].Magnitude;
            }

            int maxIndex = 0;
            double maxMagnitude = 0;
            for (int i = 1; i < magnitudes.Length - 1; i++)
            {
                if (magnitudes[i] > maxMagnitude)
                {
                    maxMagnitude = magnitudes[i];
                    maxIndex = i;
                }
            }

            Console.WriteLine($"Max Index: {maxIndex}, Max Magnitude: {maxMagnitude}");

            double interpolatedIndex = maxIndex;
            if (maxIndex > 0 && maxIndex < magnitudes.Length - 1)
            {
                double alpha = magnitudes[maxIndex - 1];
                double beta = magnitudes[maxIndex];
                double gamma = magnitudes[maxIndex + 1];
                interpolatedIndex = maxIndex + (gamma - alpha) / (2 * (2 * beta - alpha - gamma));
            }

            Console.WriteLine($"Interpolated Index: {interpolatedIndex}");

            double frequency = interpolatedIndex * sampleRate / fftSize;

            Console.WriteLine($"Calculated Frequency: {frequency} Hz");

            if (frequency < 200)
            {
                frequency *= 2;
            }

            UpdateUI(frequency);
        }
    }
}
