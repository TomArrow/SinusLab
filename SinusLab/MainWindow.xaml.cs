using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
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

        LinearAccessByteImageUnsignedHusk referenceImageHusk = null;

        private void btnImageToRaw_Click(object sender, RoutedEventArgs e)
        {
            imageToRaw();
        }

        private void imageToRaw()
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select an RGB24 image";

            if(ofd.ShowDialog() == true)
            {

                SaveFileDialog sfd = new SaveFileDialog();
                sfd.FileName = ofd.FileName + ".sinuslab.2audio32fl.raw";
                if (sfd.ShowDialog() == true)
                {

                    
                    Bitmap loadedImage = new Bitmap(1, 1);

                    try
                    {
                        loadedImage = (Bitmap)Bitmap.FromFile(ofd.FileName);
                    } catch (Exception e)
                    {
                        MessageBox.Show("Error: "+e.Message);
                        return;
                    }

                    LinearAccessByteImageUnsigned byteImg = Helpers.BitmapToLinearAccessByteArray(loadedImage);

                    referenceImageHusk = byteImg.toHusk();
                    btnRawToImage.IsEnabled = true;

                    byte[] audioData = core.RGB24ToStereo(byteImg.imageData);
                
                    File.WriteAllBytes(sfd.FileName,audioData);
                }
            }
        }

        private void btnRawToImage_Click(object sender, RoutedEventArgs e)
        {
            rawToImage();
        }

        private void rawToImage()
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select raw 32 bit float audio data coresponding to the loaded reference image";

            if (ofd.ShowDialog() == true)
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.FileName = ofd.FileName + ".sinuslab.32flaudiotorgb24.png";
                if (sfd.ShowDialog() == true)
                {
                    byte[] sourceData = File.ReadAllBytes(ofd.FileName);
                    byte[] output = core.StereoToRGB24(sourceData);
                    LinearAccessByteImageUnsigned image = new LinearAccessByteImageUnsigned(output, referenceImageHusk);
                    Bitmap imgBitmap = Helpers.ByteArrayToBitmap(image);
                    imgBitmap.Save(sfd.FileName);
                    //File.WriteAllBytes(sfd.FileName, output);
                }
            }

        }

        private void btnLoadReferenceImage_Click(object sender, RoutedEventArgs e)
        {
            loadReferenceImage();
        }

        private void loadReferenceImage()
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select an RGB24 image";

            if (ofd.ShowDialog() == true)
            {

                Bitmap loadedImage = new Bitmap(1, 1);

                try
                {
                    loadedImage = (Bitmap)Bitmap.FromFile(ofd.FileName);
                }
                catch (Exception e)
                {
                    MessageBox.Show("Error: " + e.Message);
                    return;
                }

                LinearAccessByteImageUnsigned byteImg = Helpers.BitmapToLinearAccessByteArray(loadedImage);

                referenceImageHusk = byteImg.toHusk();

                btnRawToImage.IsEnabled = true;
            }
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
