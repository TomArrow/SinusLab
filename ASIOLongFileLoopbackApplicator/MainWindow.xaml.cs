using Microsoft.Win32;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ASIOLongFileLoopbackApplicator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string selectedAsioDevice = null;

        public MainWindow()
        {
            InitializeComponent();

            asioDevice.ItemsSource = AsioOut.GetDriverNames();
            
        }



        private void asioDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedAsioDevice = (string)asioDevice.SelectedItem;
            goBtn.IsEnabled = true;
        }

        private void goBtn_Click(object sender, RoutedEventArgs e)
        {
            processAudioFile();
        }

        const int EXTRARECORDINGTIME = 10;// Extra recording time in seconds. Make sure we dont miss anything.

        private void processAudioFile()
        {
            goBtn.IsEnabled = false;
            asioDevice.IsEnabled = false;

            bool fail = true;


            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select w64 file to process";

            if (ofd.ShowDialog() == true)
            {
                SaveFileDialog sfd = new SaveFileDialog();

                string suffix = "";
                

                sfd.FileName = ofd.FileName + ".ASIOLFLA" + suffix + ".w64";
                if (sfd.ShowDialog() == true)
                {
                    SuperWAV inputAudio = new SuperWAV(ofd.FileName);
                    SuperWAV.WavInfo inInfo = inputAudio.getWavInfo();

                    if(inInfo.channelCount != 2)
                    {
                        MessageBox.Show("Only files with a channel count of 2 supported.");
                    } else
                    {

                        fail = false;

                        // Allocate an output file.
                        SuperWAV outputAudio = new SuperWAV(sfd.FileName, SuperWAV.WavFormat.WAVE64, inInfo.sampleRate, 2,inInfo.audioFormat,inInfo.bitsPerSample);

                        ConcurrentQueue<float[]> sampleBuffer = new ConcurrentQueue<float[]>();
                        float[] tmp;

                        bool processingActive = true;

                        UInt64 outputFileOffset = 0;

                        // Do actual processing.
                        AsioOut asioOut = new AsioOut(selectedAsioDevice);
                        SuperWAVProvider sampleProvider = new SuperWAVProvider(inputAudio);
                        IWaveProvider waveProvider = new SampleToWaveProvider(sampleProvider);
                        asioOut.InitRecordAndPlayback(waveProvider, 2, (int)inInfo.sampleRate);
                        asioOut.AudioAvailable += (object sender, AsioAudioAvailableEventArgs e) =>{
                            sampleBuffer.Enqueue(e.GetAsInterleavedSamples());
                        };

                        _ = Task.Run(()=>{ 

                            Task writerTask = Task.Run(()=> {
                                float[] tmp2;
                                while (processingActive)
                                {

                                    while(sampleBuffer.Count > 0)
                                    {
                                        if (!processingActive)
                                        {
                                            break;
                                        }
                                        if(sampleBuffer.TryDequeue(out tmp2))
                                        {
                                            outputAudio.writeFloatArrayFast(tmp2, outputFileOffset);
                                            outputFileOffset += (ulong)tmp2.Length / 2;
                                        }
                                    }

                                    System.Threading.Thread.Sleep(10); // Just make sure we don't eat up all CPU with constant checks of the queue, pointless
                                }
                            });
                            // Progress updates
                            Task progressTask = Task.Run(()=> {
                                while (processingActive)
                                                                                {
                                    double progress = (double)sampleProvider.InputOffset / (inputAudio.DataLengthInTicks + inInfo.sampleRate * EXTRARECORDINGTIME);
                                    Dispatcher.Invoke(() => {
                                        progressBar.Value = progress * 100.0;
                                    });
                                    if (progress > 1.0)
                                    {
                                        processingActive = false;
                                        asioOut.Stop();
                                    }
                                    System.Threading.Thread.Sleep(100);
                                }
                            });
                            writerTask.Wait();
                            progressTask.Wait();
                            Dispatcher.Invoke(() => {
                                goBtn.IsEnabled = true;
                                asioDevice.IsEnabled = true;
                            });

                        });


                        asioOut.Play(); // start recording
                    }

                }
            }
            
                    
            

            if (fail)
            {
                goBtn.IsEnabled = true;
                asioDevice.IsEnabled = true;
            }

        }
    }
}
