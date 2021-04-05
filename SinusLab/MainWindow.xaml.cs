using System;
using System.Collections.Generic;
using System.IO;
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

            quickTest();

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
                output[i] = Math.Sin(distanceRatio * i * Math.PI);
            }

            File.WriteAllBytes("test.raw",outputBytes);

        }
    }
}
