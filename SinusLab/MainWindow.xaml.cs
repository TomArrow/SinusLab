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
using Accord.Video.FFMPEG;

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

            /*SuperWAV myWav = new SuperWAV(@"test24pcm2.w64",SuperWAV.WavFormat.WAVE64,48000,2,SuperWAV.AudioFormat.LPCM,24,48000);
            myWav.checkAndIncreaseDataSize(96000);
            myWav[0] = new double[2] { 0.5, 1.0 };
            myWav[1] = new double[2] { 0.0, 0.0 };
            myWav[2] = new double[2] { -0.5, -1.0};*/
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

                    LinearAccessByteImageUnsignedNonVectorized byteImg = Helpers.BitmapToLinearAccessByteArray(loadedImage);

                    referenceImageHusk = byteImg.toHusk();
                    btnRawToImage.IsEnabled = true;
                    btnWavToImage.IsEnabled = true;

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
                    LinearAccessByteImageUnsignedNonVectorized image = new LinearAccessByteImageUnsignedNonVectorized(output, referenceImageHusk);
                    Bitmap imgBitmap = Helpers.ByteArrayToBitmap(image);
                    imgBitmap.Save(sfd.FileName);
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

                LinearAccessByteImageUnsignedNonVectorized byteImg = Helpers.BitmapToLinearAccessByteArray(loadedImage);

                referenceImageHusk = byteImg.toHusk();

                btnRawToImage.IsEnabled = true;
                btnWavToImage.IsEnabled = true;
            }
        }

        private void btnWavToImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select wav file coresponding to the loaded reference image";

            if (ofd.ShowDialog() == true)
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.FileName = ofd.FileName + ".sinuslab.32flaudiotorgb24.png";
                if (sfd.ShowDialog() == true)
                {
                    byte[] srcDataByte;
                    
                    using (SuperWAV wavFile = new SuperWAV(ofd.FileName))
                    {
                        float[] srcData = wavFile.getEntireFileAs32BitFloat();
                        srcDataByte = new byte[srcData.Length * 4];
                        Buffer.BlockCopy(srcData, 0, srcDataByte, 0, srcDataByte.Length);
                    }
                            

                    byte[] output = core.StereoToRGB24(srcDataByte);
                    LinearAccessByteImageUnsignedNonVectorized image = new LinearAccessByteImageUnsignedNonVectorized(output, referenceImageHusk);
                    Bitmap imgBitmap = Helpers.ByteArrayToBitmap(image);
                    imgBitmap.Save(sfd.FileName);
                }
            }
        }

        private void btnImageToWav_Click(object sender, RoutedEventArgs e)
        {
            imageToWav();
        }

        private void imageToWav()
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select an RGB24 image";

            if (ofd.ShowDialog() == true)
            {

                SaveFileDialog sfd = new SaveFileDialog();
                sfd.FileName = ofd.FileName + ".sinuslab.2audio16pcm.wav";
                if (sfd.ShowDialog() == true)
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

                    LinearAccessByteImageUnsignedNonVectorized byteImg = Helpers.BitmapToLinearAccessByteArray(loadedImage);

                    referenceImageHusk = byteImg.toHusk();
                    btnRawToImage.IsEnabled = true;
                    btnWavToImage.IsEnabled = true;

                    byte[] audioData = core.RGB24ToStereo(byteImg.imageData);

                    float[] audioDataFloat = new float[audioData.Length / 4];

                    Buffer.BlockCopy(audioData, 0, audioDataFloat, 0, audioData.Length);

                    using (SuperWAV myWav = new SuperWAV(sfd.FileName, SuperWAV.WavFormat.WAVE, 48000, 2, SuperWAV.AudioFormat.LPCM, 16, (UInt64)audioDataFloat.Length / 2))
                    {
                        myWav.writeFloatArray(audioDataFloat);
                    }
                        
                    //File.WriteAllBytes(sfd.FileName, audioData);
                }
            }
        }
    }
}
