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
            //double sampleRate = 384000;
            double sampleRate = 48000;
            int seconds = 30;
            float[] output = new float[(int)sampleRate*seconds];

            double frequencyLower = 4000;
            double frequencyUpper = 10000;
            double frequencyRange = frequencyUpper - frequencyLower;
            double frequencyMiddle = frequencyLower + frequencyRange / 2;

            double lastPhase = 0,phaseHere = 0, value=0;

            double frequencyHere, phaseLengthHere, phaseAdvancementHere;

            Random rnd = new Random(12345);

            for (int i = 0; i < output.Length; i++)
            {
                //frequencyHere = frequencyLower+Math.Abs((lastPhase%1) - 0.5)*frequencyRange;
                //frequencyHere = frequencyMiddle + (lastPhase%1)*Math.Sign(lastPhase)*frequencyRange;
                // works and is what I wanted but doesnt give the FFT I wanted: frequencyHere = frequencyMiddle + Math.Abs(((lastPhase+0.5)%1.0-0.5)*2)*Math.Sign(lastPhase-1)*frequencyRange/2;
                //frequencyHere = frequencyMiddle + value * frequencyRange / 2;
                //frequencyHere = frequencyMiddle + Math.Abs(((lastPhase + 0.5) % 1.0 - 0.5) * 2) * Math.Sign(lastPhase - 1) * frequencyRange / 2;
                //frequencyHere = i;
                //frequencyHere = Math.PI*1000*i; // This created perfect white noise! (addendum: actually doesnt sound perfect. but looks pretty good and measures cleanly!)
                //frequencyHere = 3143*i; // Imperfect!
                //frequencyHere = Math.PI*10000*i; // Prettier spectrum but low frequencies don't measure cleanly anymore as white noise  - nvm I was using too small a sample, it also measures perfectly.
                //frequencyHere = Math.PI*500*i; // Also measures perfectly.
                //frequencyHere = Math.PI*sampleRate*i; // Also measures perfectly. Creates a kind of diagonal grid pattern on the FFT. Rather unique sound too.
                //frequencyHere = Math.PI*sampleRate/2*i; // Also measures perfectly. Same but roughed / bigger grid.
                //frequencyHere = Math.PI*sampleRate*2*i; // Also measures perfectly. About same...
                //frequencyHere = Math.PI*1000*i; // This created perfect white noise! (addendum: actually doesnt sound perfect. but looks pretty good and measures cleanly!)
                //frequencyHere = Math.E*1000*i; // Not even temporally. But actually the spectrum is also kinda flat. hmm
                //frequencyHere = Math.PI*1000*i; // This created perfect white noise! (addendum: actually doesnt sound perfect. but looks pretty good and measures cleanly!)
                //frequencyHere = frequencyLower + rnd.NextDouble()*frequencyRange;  // This basically creates a frequency high right in the middle of that range and from there it just falls off to the sides, but no notable cuts at 4000 and 10000, it just keeps falling off at the same rate. So I guess that with the color encoding there I basically just got lucky. Since this isn't very clean and creates a lot of noise across the entire spectrum in general. hmm

                //frequencyHere = sampleRate * i;

                //frequencyHere = Math.PI*sampleRate * i;
                //frequencyHere = Math.PI*100 * i;
                //frequencyHere = Math.Sqrt(Math.PI)*1000 * i;
                //frequencyHere = Math.PI*1000 * i;
                //frequencyHere = Math.PI*sampleRate/20 * i; // Kinda interesting I guess
                frequencyHere = Math.PI*2000 * i; 
                phaseLengthHere = sampleRate/ frequencyHere / 2;
                phaseAdvancementHere = 1 / phaseLengthHere;
                phaseHere = lastPhase + phaseAdvancementHere;
                value = Math.Sin(phaseHere * Math.PI);
                output[i] = (float)(0.5 * value); // tmpV.Y is amplitude (chrominance/saturation)
                lastPhase = phaseHere % 2;

                // Math.PI*Math.PI*1000*i*2 / sampleRate

            }

            string baseSaveName = "test";
            string add = "";
            int index = 0;
            while (File.Exists(baseSaveName+"_"+index+".wav")) {
                index++;
            }

            //output = boxBlurFloat(output,10); // Interestingly does not what you'd think ... It mainly introduces gaps in the spectrum. And it produces exactly the amount of gaps that you set the radius to. But it also reduces high frequencies to a degree, but not as much as you'd think.
            //output = boxBlurFloat(output,100); 
            //output = boxBlurFloat(output,10000); 
            //output = boxBlurFloat(output,2,10); // This creates kind of a rolloff above 2 kHz. strange! 
            //output = addFloat(output,boxBlurFloat(output,2,10),-1); // MAY HAVE MISTAKE CAUSE LEFT IN FORMER LINE This creates kind of a high pass but it's not very steep and it doesnt cut around 2 kHz even tho in the boxblurred version 2khz is perfectly fine preserved.
            //output = addFloat(output,boxBlurFloat(output,1,20),-1); // MAY HAVE MISTAKE CAUSE LEFT IN FORMER LINE This seems to create a bandpass at an average of 1K and a high at around 3k maybe? Straaange!
            //output = addFloat(output,boxBlurFloat(output,2,10),-1); output = interpolateNN(output,sampleRate,sampleRate*4); // Meh ... creates quantization noise 
            //output = addFloat(output,boxBlurFloat(output,1,10),-1); bit higher cutoff frequency
            //output = addFloat(output,boxBlurFloat(output,1,10),-1); //output = interpolateNN(output,sampleRate,sampleRate*4);
            //output = addFloat(output,boxBlurFloat(output,1,10),-1); output = interpolateLinear(output,sampleRate,sampleRate*4); // Creates black frequency holes in the middle of the spectrum (probably where the division is perefct and as such no interpolation necessary?)  and near the top, other than that not great results

            // Ok so ... this doesnt seem very promising. I guess all this stuff is harder than I thought.

            using (SuperWAV myWav = new SuperWAV(baseSaveName + "_" + index + ".wav", SuperWAV.WavFormat.WAVE,(uint)sampleRate,1,SuperWAV.AudioFormat.FLOAT,32,(UInt64)output.Length))
            {
                myWav.writeFloatArrayFast(output);
            }
        }
        struct AverageHelper
        {
            public double totalValue;
            public double multiplier;
        }

        public float[] addFloat(float[] array1, float[] array2, float array2Multiplicator)
        {
            float[] output = new float[array1.Length];
            for(int i = 0; i < output.Length; i++)
            {
                output[i] = array1[i] + array2[i] * array2Multiplicator;
            }
            return output;
        }

        public float[] interpolateNN(float[] array1, double inputRate, double outputRate)
        {

            double ratio = outputRate / inputRate;
            float[] output = new float[(int)((double)array1.Length*ratio)];
            for(int i = 0; i < array1.Length; i++)
            {
                output[i] = array1[(int)Math.Round((double)i / ratio)];
            }
            return output;
        }
        public float[] interpolateLinear(float[] array1, double inputRate, double outputRate)
        {


            double ratio = outputRate / inputRate;
            float[] output = new float[(int)((double)array1.Length*ratio)];

            double positionInInput;
            double positionInInputMod;
            for(int i = 0; i < array1.Length; i++)
            {
                positionInInput = (double)i / ratio;
                positionInInputMod = positionInInput %1.0;
                output[i] = (float)((array1[(int)Math.Floor(positionInInput)]*(1-positionInInputMod) + positionInInputMod*array1[(int)Math.Ceiling(positionInInput)])); 
            }
            return output;
        }

        public float[] boxBlurFloat(float[] input, uint radius,int runs =1)
        {
            float[] output = new float[input.Length];
            for (int r = 0; r < runs; r++) { 
                AverageHelper averageHelper = new AverageHelper();
                for (int i = 0; i < input.Length; i++)
                {
                    if (i == 0)
                    {
                        for (int ii = 0; ii <= radius; ii++)
                        {
                            if ((i + ii) >= input.Length)
                            {
                                break;
                            }
                            averageHelper.totalValue += input[i + ii];
                            averageHelper.multiplier += 1;
                        }
                    }
                    else
                    {
                        if (i > radius)
                        {
                            averageHelper.totalValue -= input[i - radius - 1];
                            averageHelper.multiplier -= 1;
                        }
                        if ((i + radius) < input.Length)
                        {
                            averageHelper.totalValue += input[i + radius];
                            averageHelper.multiplier += 1;
                        }
                    }
                    output[i] = (float)(averageHelper.totalValue / averageHelper.multiplier);
                }
                input = output;
            }
            return output;
        }
    }
}
