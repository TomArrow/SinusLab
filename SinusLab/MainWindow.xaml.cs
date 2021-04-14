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

                    LinearAccessByteImageUnsignedNonVectorized byteImg = Helpers.BitmapToLinearAccessByteArraUnsignedNonVectorizedy(loadedImage);

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

                LinearAccessByteImageUnsignedNonVectorized byteImg = Helpers.BitmapToLinearAccessByteArraUnsignedNonVectorizedy(loadedImage);

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

                    LinearAccessByteImageUnsignedNonVectorized byteImg = Helpers.BitmapToLinearAccessByteArraUnsignedNonVectorizedy(loadedImage);

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

        private void btnVideoToW64_Click(object sender, RoutedEventArgs e)
        {
            videoToW64();
        }

        Accord.Math.Rational videoFrameRate = 24;
        LinearAccessByteImageUnsignedHusk videoReferenceFrame = null;

        private void videoToW64()
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select a video";

            if (ofd.ShowDialog() == true)
            {

                SaveFileDialog sfd = new SaveFileDialog();
                sfd.FileName = ofd.FileName + ".sinuslab.2audio16pcm.w64";
                if (sfd.ShowDialog() == true)
                {
                    long frameCount;
#if !DEBUG
                    try
                    {
#endif

                        VideoFileReader reader = new VideoFileReader();
                        reader.Open(ofd.FileName);

                        /*
                        Console.WriteLine("width:  " + reader.Width);
                        Console.WriteLine("height: " + reader.Height);
                        Console.WriteLine("fps:    " + reader.FrameRate);
                        Console.WriteLine("codec:  " + reader.CodecName);
                        Console.WriteLine("length:  " + reader.FrameCount);
                        */

                        frameCount = reader.FrameCount;

                        UInt64 currentFrame = 0;

                        //loadedVideo = new LinearAccessByteImage[frameCount];
                        videoFrameRate = reader.FrameRate;

                        videoReferenceFrame = null;
                        btnW64ToVideo.IsEnabled = false;

                        using (SuperWAV myWav = new SuperWAV(sfd.FileName, SuperWAV.WavFormat.WAVE64, 48000, 2, SuperWAV.AudioFormat.LPCM, 16))
                        {


                            while (true)
                            {
                                using (Bitmap videoFrame = reader.ReadVideoFrame())
                                {
                                    if (videoFrame == null)
                                        break;

                                    LinearAccessByteImageUnsignedNonVectorized frameData = Helpers.BitmapToLinearAccessByteArraUnsignedNonVectorizedy(videoFrame);

                                    if(videoReferenceFrame == null)
                                    {

                                        videoReferenceFrame = frameData.toHusk();
                                        btnW64ToVideo.IsEnabled = true;
                                    }

                                    byte[] audioData = core.RGB24ToStereo(frameData.imageData);

                                    float[] audioDataFloat = new float[audioData.Length / 4];

                                    Buffer.BlockCopy(audioData, 0, audioDataFloat, 0, audioData.Length);
                                    //myWav.writeFloatArray(audioDataFloat, currentFrame*(UInt64)audioDataFloat.Length/2);
                                    myWav.writeFloatArrayFast(audioDataFloat, currentFrame*(UInt64)audioDataFloat.Length/2);

                                    /*if (currentFrame % 1000 == 0)
                                    {
                                        progress.Report("Loading video: " + currentFrame + "/" + frameCount + " frames");
                                    }*/


                                    currentFrame++;
                                    // process the frame here

                                }
                            }
                        }

                        reader.Close();

                        // If the video delivered less frames than it promised (can happen for whatever reason) then we chip off the last parts of the array
                        /*if (currentFrame < frameCount)
                        {
                            tooFewFramesDelivered = (int)frameCount - currentFrame;
                            Array.Resize<LinearAccessByteImage>(ref loadedVideo, currentFrame);
                        }*/
#if !DEBUG
                    }
                    catch (Exception e)
                    {
                        //failed = true;
                        MessageBox.Show(e.Message);
                        videoReferenceFrame = null;
                        btnW64ToVideo.IsEnabled = false;
                    }
#endif
                }
            }
        }

        private void btnLoadReferenceVideo_Click(object sender, RoutedEventArgs e)
        {
            loadReferenceVideo();
        }

        private void loadReferenceVideo()
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select a video";

            if (ofd.ShowDialog() == true)
            {

                long frameCount;
                try
                {

                    VideoFileReader reader = new VideoFileReader();
                    reader.Open(ofd.FileName);

                    /*
                    Console.WriteLine("width:  " + reader.Width);
                    Console.WriteLine("height: " + reader.Height);
                    Console.WriteLine("fps:    " + reader.FrameRate);
                    Console.WriteLine("codec:  " + reader.CodecName);
                    Console.WriteLine("length:  " + reader.FrameCount);
                    */

                    frameCount = reader.FrameCount;


                    //loadedVideo = new LinearAccessByteImage[frameCount];
                    videoFrameRate = reader.FrameRate;


                    using (Bitmap videoFrame = reader.ReadVideoFrame())
                    {
                        if (videoFrame == null)
                            throw new Exception("No video frame in video.");

                        LinearAccessByteImageUnsignedNonVectorized frameData = Helpers.BitmapToLinearAccessByteArraUnsignedNonVectorizedy(videoFrame);


                        videoReferenceFrame = frameData.toHusk();
                        btnW64ToVideo.IsEnabled = true;

                        /*if (currentFrame % 1000 == 0)
                        {
                            progress.Report("Loading video: " + currentFrame + "/" + frameCount + " frames");
                        }*/


                        // process the frame here

                    }

                    reader.Close();

                    // If the video delivered less frames than it promised (can happen for whatever reason) then we chip off the last parts of the array
                    /*if (currentFrame < frameCount)
                    {
                        tooFewFramesDelivered = (int)frameCount - currentFrame;
                        Array.Resize<LinearAccessByteImage>(ref loadedVideo, currentFrame);
                    }*/

                }
                catch (Exception e)
                {
                    //failed = true;
                    MessageBox.Show(e.Message);
                    videoReferenceFrame = null;
                    btnW64ToVideo.IsEnabled = false;
                }
                
            }
        }

        private void btnW64ToVideo_Click(object sender, RoutedEventArgs e)
        {
            w64ToVideo();
        }

        private void w64ToVideo()
        {
            if(videoReferenceFrame == null)
            {
                MessageBox.Show("No reference video loaded.");
                return;
            }

            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select w64 file coresponding to the loaded reference video";

            if (ofd.ShowDialog() == true)
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.FileName = ofd.FileName + ".sinuslab.32flaudiotorgb24.mkv";
                if (sfd.ShowDialog() == true)
                {


                    UInt64 frameCount;
#if !DEBUG
                    try
                    {
#endif
                        using (SuperWAV wavFile = new SuperWAV(ofd.FileName))
                        {
                            //float[] srcData = wavFile.getEntireFileAs32BitFloat();
                            //srcDataByte = new byte[srcData.Length * 4];
                            //Buffer.BlockCopy(srcData, 0, srcDataByte, 0, srcDataByte.Length);
                        
                            VideoFileWriter writer = new VideoFileWriter();
                            writer.Open(sfd.FileName, videoReferenceFrame.width, videoReferenceFrame.height, videoFrameRate, VideoCodec.FFV1);

                            /*
                            Console.WriteLine("width:  " + reader.Width);
                            Console.WriteLine("height: " + reader.Height);
                            Console.WriteLine("fps:    " + reader.FrameRate);
                            Console.WriteLine("codec:  " + reader.CodecName);
                            Console.WriteLine("length:  " + reader.FrameCount);
                            */

                            UInt64 imageLength = (UInt64)videoReferenceFrame.width * (UInt64)videoReferenceFrame.height;
                            frameCount = wavFile.DataLengthInTicks/(imageLength);

                            byte[] srcDataByte;
                            byte[] output;
                            Bitmap imgBitmap; 
                            float[] srcData;
                            LinearAccessByteImageUnsignedNonVectorized image;

                            //int currentFrame = 0;

                            //int frameToWrite = 0;
                            for (UInt64 i = 0; i < frameCount; i++)
                            {
                                //srcData = wavFile.getAs32BitFloat(imageLength*i, imageLength*(i+1)-1); 
                                srcData = wavFile.getAs32BitFloatFast(imageLength*i, imageLength*(i+1)-1); 
                                srcDataByte = new byte[srcData.Length * 4]; 
                                Buffer.BlockCopy(srcData, 0, srcDataByte, 0, srcDataByte.Length);
                                output = core.StereoToRGB24(srcDataByte);
                                image = new LinearAccessByteImageUnsignedNonVectorized(output, videoReferenceFrame);
                                imgBitmap = Helpers.ByteArrayToBitmap(image);
                                writer.WriteVideoFrame(imgBitmap,(uint)i);
                                writer.Flush();
                                /*if (currentFrame % 1000 == 0)
                                {
                                    progress.Report("Saving video: " + i + "/" + frameCount + " frames");
                                }*/
                            }

                            writer.Close();
                        }
#if !DEBUG
                    }
                    catch (Exception e)
                    {
                       MessageBox.Show(e.Message);
                    }
#endif
                   
                }
            }
        }
    }
}
