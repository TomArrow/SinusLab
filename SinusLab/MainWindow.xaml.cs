using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
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

namespace SinusLab
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            //quickTest();
            //imgTest2();
            imgTest2_reverse();

            Close();
        }

        private void quickTest()
        {

            int samplerate = 48000;
            double amplitude = 0.5;
            int frequencyToGenerate = 500; // Must be smaller than samplerate/2
            double distanceRatio = 2*(double)frequencyToGenerate/ (double)samplerate; 
            int length = 10;
            double[] output = new double[samplerate * length];

            double lastValue = 0;
            output[0] = 0;
            for(int i = 1; i < output.Length; i++)
            {
                output[i] = amplitude* Math.Sin(distanceRatio*(double)i*Math.PI);
            }

            byte[] outputBytes = new byte[output.Length*4];
            byte[] tmp;
            for (int i = 1; i < output.Length; i++)
            {
                tmp = BitConverter.GetBytes((float)output[i]);
                Array.Copy(tmp, 0, outputBytes, i * 4, 4);
            }

            File.WriteAllBytes("test.raw",outputBytes);

        }

        private void imgTest2()
        {
            byte[] sourceData = File.ReadAllBytes("IMG_5518.raw"); // 8 bit image

            int samplerate = 48000;
            double maxAmplitude = Math.Sqrt(2.0) / 2.0;

            int lowerFrequency = 500;
            int upperFrequency = 20000;
            double frequencyRange = upperFrequency - lowerFrequency;

            //double distanceRatio = 2*(double)frequencyToGenerate/ (double)samplerate; 
            double[] output = new double[sourceData.Length/3];
            double[] outputL = new double[sourceData.Length/3];

            double lastPhase = 0;
            double frequencyHere;
            double phaseLengthHere;
            double phaseAdvancementHere;
            double phaseHere;
            double hueTo0to1Range;
            output[0] = 0;
            Vector3 tmpV;
            for(int i = 1; i < output.Length; i++)
            {
                tmpV.X = (float)((double)sourceData[i * 3] ); // R
                tmpV.Y = (float)((double)sourceData[i * 3+1] );  // G
                tmpV.Z = (float)((double)sourceData[i * 3+2]); // B

                tmpV = Helpers.sRGBToCIELChab(tmpV); //cielchab = Luma, chroma, hue. hue is frequency, chroma is amplitude

                hueTo0to1Range = ((double)tmpV.Z + Math.PI / 2) / Math.PI;
                frequencyHere = lowerFrequency + frequencyRange * hueTo0to1Range; // Hue
                phaseLengthHere = (double)samplerate/frequencyHere;
                phaseAdvancementHere = 1 / phaseLengthHere;
                phaseHere = lastPhase + phaseAdvancementHere;
                output[i] = (double)tmpV.Y / 100.0 * maxAmplitude * Math.Sin(phaseHere*Math.PI); // tmpV.Y is amplitude (chrominance/saturation)
                outputL[i] = ((double)tmpV.X-50)*2 / 100.0 * maxAmplitude; // tmpV.Y is amplitude (chrominance/saturation)
                lastPhase = phaseHere % 2;
            }

            byte[] outputBytes = new byte[output.Length*4*2];
            byte[] tmp;
            for (int i = 1; i < output.Length; i++)
            {
                tmp = BitConverter.GetBytes((float)outputL[i]);
                Array.Copy(tmp, 0, outputBytes, i * 4 *2, 4);
                tmp = BitConverter.GetBytes((float)output[i]);
                Array.Copy(tmp, 0, outputBytes, i * 4 *2 + 4, 4);
            }

            File.WriteAllBytes("test-img3stereo2.raw",outputBytes);

        }
        private void imgTest2_reverse()
        {
            byte[] sourceData = File.ReadAllBytes("test-img3stereo2.raw"); // 8 bit image
            int samplerate = 48000;

            int lowerFrequency = 500;
            int upperFrequency = 20000;
            double frequencyRange = upperFrequency - lowerFrequency;

            int windowSize = 64;


            double[] decode = new double[sourceData.Length / 8 + windowSize]; // leave windowSize amount of zeros at beginning to avoid if later.
            double[] decodeL = new double[sourceData.Length / 8];

            for(int i = 0; i < decodeL.Length; i++)
            {
                decodeL[i] = BitConverter.ToSingle(sourceData, i * 4 * 2);
                decode[i + windowSize] = BitConverter.ToSingle(sourceData, i * 4 * 2+4);
            }

            double[] audioPart = new double[windowSize];

            double[] freqs;

            //double[] c = new double[decodeL.Length];
            //double[] h = new double[decodeL.Length];

            byte[] output = new byte[decodeL.Length * 3];


            double[] fftMagnitude = FftSharp.Transform.FFTmagnitude(audioPart);
            freqs = FftSharp.Transform.FFTfreq(samplerate, fftMagnitude.Length);

            double tmpMaxIntensity = 0;
            int tmpMaxIntensityIndex = 0;

            double peakFrequencyHere = 0;
            double hue = 0;

            Vector3 tmpV;

            // decode c,h components
            for (int i = 0; i < decodeL.Length; i++)
            {
                Array.Copy(decode, i, audioPart, 0, windowSize);
                double[] window = FftSharp.Window.Hanning(audioPart.Length);
                FftSharp.Window.ApplyInPlace(window, audioPart);
                fftMagnitude = FftSharp.Transform.FFTmagnitude(audioPart);

                tmpMaxIntensity = 0;
                // find biggest frequency
                for (int b = 0; b < freqs.Length; b++)
                {
                    if(fftMagnitude[b] > tmpMaxIntensity)
                    {
                        tmpMaxIntensity = fftMagnitude[b];
                        tmpMaxIntensityIndex = b;
                    }
                }


                if(tmpMaxIntensityIndex == 0)
                {
                    peakFrequencyHere = (freqs[0]*fftMagnitude[0]+freqs[1] * fftMagnitude[1])/(fftMagnitude[0]+ fftMagnitude[1]);
                } else if (tmpMaxIntensityIndex == freqs.Length - 1)
                {
                    peakFrequencyHere = (freqs[freqs.Length - 1] * fftMagnitude[freqs.Length - 1] + freqs[freqs.Length - 2] * fftMagnitude[freqs.Length - 2]) / (fftMagnitude[freqs.Length - 1] + fftMagnitude[freqs.Length - 2]);
                }
                else
                {
                    peakFrequencyHere = (freqs[tmpMaxIntensityIndex - 1] * fftMagnitude[tmpMaxIntensityIndex - 1] + freqs[tmpMaxIntensityIndex ] * fftMagnitude[tmpMaxIntensityIndex ] +freqs[tmpMaxIntensityIndex +1] * fftMagnitude[tmpMaxIntensityIndex + 1]) / (fftMagnitude[tmpMaxIntensityIndex - 1]+ fftMagnitude[tmpMaxIntensityIndex ]+ fftMagnitude[tmpMaxIntensityIndex + 1]);

                }

                hue = (((peakFrequencyHere-lowerFrequency)/frequencyRange)*Math.PI)-Math.PI/2;

                tmpV.X = (float)(decodeL[i]/2+0.5)*100;
                tmpV.Y = (float)Math.Sqrt(tmpMaxIntensity)*100; //experimental * 4, normally doesnt beong there.
                tmpV.Z = (float)hue;

                tmpV = Helpers.CIELChabTosRGB(tmpV);

                output[i * 3] = (byte)Math.Min(255,Math.Max(0,tmpV.X));
                output[i * 3+1] = (byte)Math.Min(255,Math.Max(0,tmpV.Y));
                output[i * 3+2] = (byte)Math.Min(255,Math.Max(0,tmpV.Z));
            }

            File.WriteAllBytes("test-img3stereo2-backtoIMG4.raw",output);
        }

        private void imgTest()
        {
            byte[] sourceData = File.ReadAllBytes("IMG_5518.raw"); // 8 bit image

            int samplerate = 48000;
            double amplitude = Math.Sqrt(2.0) / 2.0;

            int lowerFrequency = 500;
            int upperFrequency = 20000;
            double frequencyRange = upperFrequency - lowerFrequency;

            //double distanceRatio = 2*(double)frequencyToGenerate/ (double)samplerate; 
            double[] output = new double[sourceData.Length/3];

            double lastPhase = 0;
            double frequencyHere;
            double phaseLengthHere;
            double phaseAdvancementHere;
            double phaseHere;
            output[0] = 0;
            for(int i = 1; i < output.Length; i++)
            {
                frequencyHere = lowerFrequency + frequencyRange * (double)sourceData[i*3] / 255.0;
                phaseLengthHere = (double)samplerate/frequencyHere;
                phaseAdvancementHere = 1 / phaseLengthHere;
                phaseHere = lastPhase + phaseAdvancementHere;
                output[i] = amplitude* Math.Sin(phaseHere*Math.PI);
                lastPhase = phaseHere % 2;
            }

            byte[] outputBytes = new byte[output.Length*4];
            byte[] tmp;
            for (int i = 1; i < output.Length; i++)
            {
                tmp = BitConverter.GetBytes((float)output[i]);
                Array.Copy(tmp, 0, outputBytes, i * 4, 4);
            }

            File.WriteAllBytes("test-img.raw",outputBytes);

        }
    }
}
