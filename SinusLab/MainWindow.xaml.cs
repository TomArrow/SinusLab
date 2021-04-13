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

        SinusLabCore core = new SinusLabCore();

        public MainWindow()
        {
            InitializeComponent();

            //quickTest();
            //imgTest2();
            //imgTest2_reverse();

            //Close();
        }




        /*
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
            //byte[] sourceData = File.ReadAllBytes("IMG_5518.raw"); // 8 bit image
            byte[] sourceData = File.ReadAllBytes("crowder.raw"); // 8 bit image

            byte[] outputBytes = core.RGB24ToStereo(sourceData);

            File.WriteAllBytes("test-img4stereo6-pifix.raw",outputBytes);

        }
        private void imgTest2_reverse()
        {
            byte[] sourceData = File.ReadAllBytes("test-img4stereo6-pifix.raw"); // 8 bit image

            byte[] output = core.StereoToRGB24(sourceData);

            File.WriteAllBytes("test-img4stereo6-backtoIMG22-pifix.raw", output);
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
        */
    }
}
