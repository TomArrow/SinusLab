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

namespace Experiments
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            test();
            Close();
        }

        private void test()
        {
            double sampleRate = 384000;
            int seconds = 10;
            float[] output = new float[(int)sampleRate*seconds];

            double frequencyLower = 500;
            double frequencyUpper = 40000;
            double frequencyRange = frequencyUpper - frequencyLower;
            double frequencyMiddle = frequencyLower + frequencyRange / 2;

            double lastPhase = 0,phaseHere = 0;

            double frequencyHere, phaseLengthHere, phaseAdvancementHere;
            for (int i = 0; i < output.Length; i++)
            {
                //frequencyHere = frequencyLower+Math.Abs((lastPhase%1) - 0.5)*frequencyRange;
                frequencyHere = frequencyMiddle + ((lastPhase+0.5)%1.0-0.5)*2* Math.Sign(lastPhase)*frequencyRange;
                phaseLengthHere = sampleRate/ frequencyHere / 2;
                phaseAdvancementHere = 1 / phaseLengthHere;
                phaseHere = lastPhase + phaseAdvancementHere;
                output[i] = (float)(0.5* Math.Sin(phaseHere * Math.PI)); // tmpV.Y is amplitude (chrominance/saturation)
                lastPhase = phaseHere % 2;
            }

            string baseSaveName = "test";
            string add = "";
            int index = 0;
            while (File.Exists(baseSaveName+"_"+index+".wav")) {
                index++;
            }

            using (SuperWAV myWav = new SuperWAV(baseSaveName + "_" + index + ".wav", SuperWAV.WavFormat.WAVE,(uint)sampleRate,1,SuperWAV.AudioFormat.FLOAT,32,(UInt64)output.Length))
            {
                myWav.writeFloatArrayFast(output);
            }
        }
    }
}
