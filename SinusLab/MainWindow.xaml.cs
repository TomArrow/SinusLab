using Accord.Video.FFMPEG;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace SinusLab
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        SinusLabCore core = new SinusLabCore();

        uint maxThreads = 4;

        private bool isInitialized = false;
        public MainWindow()
        {
            InitializeComponent();

            /*SuperWAV myWav = new SuperWAV(@"test24pcm2.w64",SuperWAV.WavFormat.WAVE64,48000,2,SuperWAV.AudioFormat.LPCM,24,48000);
            myWav.checkAndIncreaseDataSize(96000);
            myWav[0] = new double[2] { 0.5, 1.0 };
            myWav[1] = new double[2] { 0.0, 0.0 };
            myWav[2] = new double[2] { -0.5, -1.0};*/

            //ThreadTests();

            isInitialized = true;
            maxThreads = (uint)Environment.ProcessorCount;
            txtMaxThreads.Text = Environment.ProcessorCount.ToString();
            lblMaxThreads.Content = "("+ Environment.ProcessorCount.ToString() + " cores)";

            cmbFFTWindowFunction.ItemsSource = System.Enum.GetValues(typeof(SinusLabCore.WindowFunction));
            cmbFFTWindowFunction.SelectedItem = core.windowFunction;
        }

        ~MainWindow()
        {
            if (previewWaveSource != null)
            {
                previewWaveSource.Dispose();
            }
        }

        private async void ThreadTests()
        {
            // Gives 3 times the result "3"
            /*for(int i = 0; i < 3; i++)
            {
                Task.Run(() => {
                    Dispatcher.Invoke(()=> {
                        MessageBox.Show(i.ToString());
                    });
                });
            }*/

            /*for (int i = 0; i < 3; i++)
            {
                Task.Run(blah(i));
            }*//*
            for(int i = 0; i < 3; i++)
            {
                Task.Run(((int a)=>  {
                    return (Action)(()=>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(a.ToString());
                        });
                    });
                })(i));
            }*/
            /*
            Func<int,Action> blah = ((int a ) => {
                return (Action)(() => {
                    System.Threading.Thread.Sleep(2500);
                    Dispatcher.Invoke(() => {
                        MessageBox.Show(a.ToString());
                    });
                });
            });

            for (int i = 0; i < 3; i++)
            {
                Task.Run(blah(i));
            }*/

            // Works:
            /*
            for (int i = 0; i < 3; i++)
            {
                Task.Run(((Func<int,Action>)(((int a) => {
                    return (() => {
                        System.Threading.Thread.Sleep(2500);
                        Dispatcher.Invoke(() => {
                            MessageBox.Show(a.ToString());
                        });
                    });
                })))(i));
            }*/

        }
        /*
        public Action blah(int abc)
        {
            return () => {
                System.Threading.Thread.Sleep(2500);
                Dispatcher.Invoke(() => {
                    MessageBox.Show(abc.ToString());
                });
            };
        }*/

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
                    //btnRawToImage.IsEnabled = true;
                    //btnWavToImage.IsEnabled = true;
                    //btnWavToImageFast.IsEnabled = true;
                    buttonsWavToImage.IsEnabled = true;

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
                    byte[] output = core.StereoToRGB24(sourceData,core.samplerate); // For raw we take the value from the user setting.
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

                //btnRawToImage.IsEnabled = true;
                //btnWavToImage.IsEnabled = true;
                //btnWavToImageFast.IsEnabled = true;

                buttonsWavToImage.IsEnabled = true;
            }
        }

        private void btnWavToImage_Click(object sender, RoutedEventArgs e)
        {
            wavToImage();
        }
        
        private void wavToImage(bool fast = false,SinusLabCore.FormatVersion formatVersion = SinusLabCore.FormatVersion.DEFAULT_LEGACY,bool superHighQuality = false)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select wav file coresponding to the loaded reference image";

            if (ofd.ShowDialog() == true)
            {
                SaveFileDialog sfd = new SaveFileDialog();

                string suffix = "";
                switch (formatVersion)
                {
                    case SinusLabCore.FormatVersion.V2:
                        suffix = "V2";
                        if (superHighQuality)
                        {
                            suffix += "UHQ";
                        }
                        break;
                    case SinusLabCore.FormatVersion.V2_NOLUMA_DECODE_ONLY:
                        suffix = "V2noLum";
                        break;
                    case SinusLabCore.FormatVersion.DEFAULT_LEGACY:
                    default:
                        suffix = "V1";
                        break;
                }

                sfd.FileName = ofd.FileName + ".sinuslab.32flaudiotorgb24"+ suffix + ".png";
                if (sfd.ShowDialog() == true)
                {
                    byte[] srcDataByte;

                    uint sampleRate = 48000;
                    using (SuperWAV wavFile = new SuperWAV(ofd.FileName))
                    {
                        sampleRate = wavFile.getWavInfo().sampleRate;
                        //float[] srcData = wavFile.getEntireFileAs32BitFloat();
                        float[] srcData = wavFile.getAs32BitFloatFast(0,(UInt64)referenceImageHusk.width* (UInt64)referenceImageHusk.height-1);
                        srcDataByte = new byte[srcData.Length * 4];
                        Buffer.BlockCopy(srcData, 0, srcDataByte, 0, srcDataByte.Length);
                    }

                    SpeedReport mySpeedReport = new SpeedReport();
                    mySpeedReport.setPrefix("wavToImage");
                    byte[] output;
                    switch (formatVersion)
                    {
                        case SinusLabCore.FormatVersion.V2_NOLUMA_DECODE_ONLY:
                            output = fast ? core.StereoToRGB24V2Fast(srcDataByte, sampleRate, false, false, 0.5, speedReport: mySpeedReport) : core.StereoToRGB24V2(srcDataByte, sampleRate, false, false);  // Super high quality doesn't apply here because no LF luma decoding.
                            break;
                        case SinusLabCore.FormatVersion.V2:
                            output = fast ? core.StereoToRGB24V2Fast(srcDataByte, sampleRate, true, superHighQuality, 0.5, speedReport: mySpeedReport) : core.StereoToRGB24V2(srcDataByte, sampleRate, true, superHighQuality);
                            break;
                        case SinusLabCore.FormatVersion.DEFAULT_LEGACY:
                        default:
                            output = fast ? core.StereoToRGB24Fast(srcDataByte, sampleRate) : core.StereoToRGB24(srcDataByte, sampleRate);
                            break;
                    }
                    mySpeedReport.setPrefix("wavToImage");
                    mySpeedReport.logEvent("Processing done.");


                    LinearAccessByteImageUnsignedNonVectorized image = new LinearAccessByteImageUnsignedNonVectorized(output, referenceImageHusk);
                    Bitmap imgBitmap = Helpers.ByteArrayToBitmap(image);
                    imgBitmap.Save(sfd.FileName);

                    mySpeedReport.logEvent("Converted to Bitmap & saved");
                    mySpeedReport.Stop();
                    MessageBox.Show(mySpeedReport.getFormattedList());
                }
            }
        }

        private void btnImageToWav_Click(object sender, RoutedEventArgs e)
        {
            imageToWav();
        }

        private void imageToWav(SinusLabCore.FormatVersion formatVersion = SinusLabCore.FormatVersion.DEFAULT_LEGACY)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select an RGB24 image";

            if (ofd.ShowDialog() == true)
            {

                SaveFileDialog sfd = new SaveFileDialog();

                string suffix = "";
                switch (formatVersion)
                {
                    case SinusLabCore.FormatVersion.V2:
                        suffix = "V2";
                        break;
                    case SinusLabCore.FormatVersion.DEFAULT_LEGACY:
                    default:
                        suffix = "V1";
                        break;
                }

                sfd.FileName = ofd.FileName + ".sinuslab.2audio16pcm"+ suffix + ".wav";
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
                    //btnRawToImage.IsEnabled = true;
                    //btnWavToImage.IsEnabled = true;
                    //btnWavToImageFast.IsEnabled = true;
                    buttonsWavToImage.IsEnabled = true;

                    byte[] audioData;
                    switch (formatVersion)
                    {
                        case SinusLabCore.FormatVersion.V2:
                            audioData = core.RGB24ToStereoV2(byteImg.imageData);
                            break;
                        case SinusLabCore.FormatVersion.DEFAULT_LEGACY:
                        default:
                            audioData = core.RGB24ToStereo(byteImg.imageData);
                            break;
                    }

                    float[] audioDataFloat = new float[audioData.Length / 4];

                    Buffer.BlockCopy(audioData, 0, audioDataFloat, 0, audioData.Length);

                    using (SuperWAV myWav = new SuperWAV(sfd.FileName, SuperWAV.WavFormat.WAVE, (uint) core.samplerate, 2, SuperWAV.AudioFormat.LPCM, 16, (UInt64)audioDataFloat.Length / 2))
                    {
                        //myWav.writeFloatArray(audioDataFloat);
                        myWav.writeFloatArrayFast(audioDataFloat);
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

        private void videoToW64(SinusLabCore.FormatVersion formatVersion = SinusLabCore.FormatVersion.DEFAULT_LEGACY)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select a video";

            if (ofd.ShowDialog() == true)
            {

                SuperWAV audioInput = null;
                bool hasInputAudio = false;

                if(formatVersion == SinusLabCore.FormatVersion.V3)
                {
                    OpenFileDialog ofd2 = new OpenFileDialog();
                    ofd2.Title = "Select input audio in sync with the input video. Must be .wav, w64 or rf64";

                    if(ofd2.ShowDialog() == true)
                    {
                        audioInput = new SuperWAV(ofd2.FileName);
                        hasInputAudio = true;
                    }
                }


                SaveFileDialog sfd = new SaveFileDialog();

                string suffix = "";
                switch (formatVersion)
                {
                    case SinusLabCore.FormatVersion.V3:
                        suffix = "V3";
                        break;
                    case SinusLabCore.FormatVersion.V2:
                        suffix = "V2";
                        break;
                    case SinusLabCore.FormatVersion.DEFAULT_LEGACY:
                    default:
                        suffix = "V1";
                        break;
                }

                sfd.FileName = ofd.FileName + ".sinuslab.2audio16pcm"+ suffix + ".w64";
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

                        //videoReferenceFrame = null;
                        //btnW64ToVideo.IsEnabled = false;
                        //btnW64ToVideoFast.IsEnabled = false;
                        //btnW64ToVideoFastAsync.IsEnabled = false;
                        buttonsAudioToVideo.IsEnabled = false;

                        using (SuperWAV myWav = new SuperWAV(sfd.FileName, SuperWAV.WavFormat.WAVE64, (uint)core.samplerate, 2, SuperWAV.AudioFormat.LPCM, 16))
                        {

                            double audioSamplesPerFrame = 2;
                            if (hasInputAudio)
                            {
                                audioSamplesPerFrame = audioInput.getWavInfo().sampleRate / videoFrameRate.ToDouble() *2; // *2 because half of previous and next frame included.
                            }
                            uint audioSamplesPerFrameHalfUInt = (uint)audioSamplesPerFrame/2;
                            uint audioSamplesToDeliverPerFrameUInt = audioSamplesPerFrameHalfUInt * 4;

                            float[] audioToEncode = new float[audioSamplesToDeliverPerFrameUInt];
                            float[] audioReadBuffer;
                            Int64 firstSampleToReadTmp =0, lastSampleToReadTmp=0;
                            UInt64 firstSampleToRead =0, lastSampleToRead=0,audioOffset=0;

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
                                        //btnW64ToVideo.IsEnabled = true;
                                        //btnW64ToVideoFast.IsEnabled = true;
                                        //btnW64ToVideoFastAsync.IsEnabled = true;
                                        buttonsAudioToVideo.IsEnabled = true;
                                    }

                                    if (hasInputAudio) {

                                        firstSampleToReadTmp = (Int64)((double)currentFrame*audioSamplesPerFrame) - audioSamplesPerFrameHalfUInt;
                                        lastSampleToReadTmp = firstSampleToReadTmp + audioSamplesToDeliverPerFrameUInt-1;
                                        audioOffset = 0;
                                        if(firstSampleToRead < 0)
                                        {
                                            audioOffset = 0 - firstSampleToRead;
                                            firstSampleToRead = 0;
                                        } 
                                        if (firstSampleToRead > audioInput.DataLengthInTicks-1)
                                        {
                                            // Yeah this is over... just do an empty array.
                                            audioToEncode = new float[audioSamplesToDeliverPerFrameUInt];
                                        } else
                                        {
                                            if (lastSampleToReadTmp < 0)
                                            {
                                                throw new Exception("This is impossible!");
                                            }
                                            lastSampleToRead = Math.Min((UInt64)lastSampleToReadTmp,audioInput.DataLengthInTicks-1);

                                            audioReadBuffer = audioInput.getAs32BitFloatFast(firstSampleToRead, lastSampleToRead);
                                            Array.Copy(audioReadBuffer,0,audioToEncode, (int)audioOffset,audioReadBuffer.Length);
                                        }


                                    }

                                    byte[] audioData = new byte[1];
                                    switch (formatVersion)
                                    {
                                        case SinusLabCore.FormatVersion.V3:
                                            audioData = hasInputAudio? core.RGB24ToStereoV2(frameData.imageData, true,audioToEncode): core.RGB24ToStereoV2(frameData.imageData,true);
                                            break;
                                        case SinusLabCore.FormatVersion.V2:
                                            audioData = core.RGB24ToStereoV2(frameData.imageData);
                                            break;
                                        case SinusLabCore.FormatVersion.DEFAULT_LEGACY:
                                        default:
                                            audioData = core.RGB24ToStereo(frameData.imageData);
                                            break;
                                    }

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
                        btnW64ToVideoFast.IsEnabled = false;
                        btnW64ToVideoFastAsync.IsEnabled = false;
                    }
#endif
                }


                if(audioInput != null)
                {
                    audioInput.Dispose();
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
                        //btnW64ToVideo.IsEnabled = true;
                        //btnW64ToVideoFast.IsEnabled = true;
                        //btnW64ToVideoFastAsync.IsEnabled = true;
                        buttonsAudioToVideo.IsEnabled = true;

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
                    //btnW64ToVideo.IsEnabled = false;
                    //btnW64ToVideoFast.IsEnabled = false;
                    //btnW64ToVideoFastAsync.IsEnabled = false;
                    buttonsAudioToVideo.IsEnabled = false;
                }
                
            }
        }

        private void btnW64ToVideo_Click(object sender, RoutedEventArgs e)
        {
            w64ToVideo();
        }

        private void w64ToVideo(bool fast = false,SinusLabCore.FormatVersion formatVersion = SinusLabCore.FormatVersion.DEFAULT_LEGACY,bool superHighQuality = false)
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

                string suffix = "";
                switch (formatVersion)
                {
                    case SinusLabCore.FormatVersion.V2:
                        suffix = "V2";
                        if (superHighQuality)
                        {
                            suffix += "UHQ";
                        }
                        break;
                    case SinusLabCore.FormatVersion.V2_NOLUMA_DECODE_ONLY:
                        suffix = "V2noLum";
                        break;
                    case SinusLabCore.FormatVersion.DEFAULT_LEGACY:
                    default:
                        suffix = "V1";
                        break;
                }

                sfd.FileName = ofd.FileName + ".sinuslab.audiotorgb24"+ suffix + ".mkv";
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

                            uint sampleRate = wavFile.getWavInfo().sampleRate;

                            //int frameToWrite = 0;
                            for (UInt64 i = 0; i < frameCount; i++)
                            {
                                //srcData = wavFile.getAs32BitFloat(imageLength*i, imageLength*(i+1)-1); 
                                srcData = wavFile.getAs32BitFloatFast(imageLength*i, imageLength*(i+1)-1); 
                                srcDataByte = new byte[srcData.Length * 4]; 
                                Buffer.BlockCopy(srcData, 0, srcDataByte, 0, srcDataByte.Length);
                                switch (formatVersion)
                                {
                                    case SinusLabCore.FormatVersion.V2_NOLUMA_DECODE_ONLY:
                                        output = fast ? core.StereoToRGB24V2Fast(srcDataByte, sampleRate, false) : core.StereoToRGB24V2(srcDataByte, sampleRate, false); // Super high quality is really irrelevant here because it doesn't do LF luma decoding
                                        break;
                                    case SinusLabCore.FormatVersion.V2:
                                        output = fast ? core.StereoToRGB24V2Fast(srcDataByte, sampleRate, true, superHighQuality) : core.StereoToRGB24V2(srcDataByte, sampleRate, true, superHighQuality);
                                        break;
                                    case SinusLabCore.FormatVersion.DEFAULT_LEGACY:
                                    default:
                                        output = fast ? core.StereoToRGB24Fast(srcDataByte, sampleRate) : core.StereoToRGB24(srcDataByte, sampleRate);
                                        break;
                                }
                                //output = fast ? core.StereoToRGB24Fast(srcDataByte, sampleRate) : core.StereoToRGB24(srcDataByte, sampleRate);
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
        
        private async void w64ToVideoMultiThreadedAsync(bool fast = false,SinusLabCore.FormatVersion formatVersion = SinusLabCore.FormatVersion.DEFAULT_LEGACY,bool superHighQuality = false)
        {
            if(videoReferenceFrame == null)
            {
                MessageBox.Show("No reference video loaded.");
                return;
            }

            int threads = maxThreads > 0 ? (int)maxThreads : Environment.ProcessorCount;
            int bufferSize = threads * 2;
            int mainLoopTimeout = 200;

            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select w64 file coresponding to the loaded reference video";

            if (ofd.ShowDialog() == true)
            {
                SaveFileDialog sfd = new SaveFileDialog();

                string suffix = "";
                switch (formatVersion)
                {
                    case SinusLabCore.FormatVersion.V2:
                        suffix = "V2";
                        if (superHighQuality)
                        {
                            suffix += "UHQ";
                        }
                        break;
                    case SinusLabCore.FormatVersion.V2_NOLUMA_DECODE_ONLY:
                        suffix = "V2noLum";
                        break;
                    case SinusLabCore.FormatVersion.DEFAULT_LEGACY:
                    default:
                        suffix = "V1";
                        break;
                }
                

                sfd.FileName = ofd.FileName + ".sinuslab.32flaudiotorgb24"+suffix+".mkv";
                if (sfd.ShowDialog() == true)
                {

                    ConcurrentDictionary<int, Bitmap> writeBuffer = new ConcurrentDictionary<int, Bitmap>();
                    bool[] imagesProcessed = new bool[1];
                    bool[] imagesProcessing = new bool[1];

                    uint sampleRate = 48000;

                    Func<float[],int,bool, Action> convertWorker = ((float[] audioData,int index,bool fastMode) => {
                        return (Action)(() => {
                            // Actual code:
                            byte[] srcDataByte = new byte[audioData.Length * 4];
                            Buffer.BlockCopy(audioData, 0, srcDataByte, 0, srcDataByte.Length);
                            audioData = null;

                            byte[] output;
                            switch (formatVersion)
                            {
                                case SinusLabCore.FormatVersion.V2_NOLUMA_DECODE_ONLY:
                                    output = fast ? core.StereoToRGB24V2Fast(srcDataByte, sampleRate, false) : core.StereoToRGB24V2(srcDataByte, sampleRate, false); // Super high quality is really irrelevant here because it doesn't do LF luma decoding
                                    break;
                                case SinusLabCore.FormatVersion.V2:
                                    //output = fast ? core.StereoToRGB24V2Fast(srcDataByte, sampleRate, true, superHighQuality) : core.StereoToRGB24V2(srcDataByte, sampleRate, true, superHighQuality);
                                    output = fast ? core.StereoToRGB24V2Fast(srcDataByte, sampleRate, true, superHighQuality) : core.StereoToRGB24V2Fast(srcDataByte, sampleRate, true, superHighQuality, -1);
                                    break;
                                case SinusLabCore.FormatVersion.DEFAULT_LEGACY:
                                default:
                                    output = fast ? core.StereoToRGB24Fast(srcDataByte, sampleRate) : core.StereoToRGB24(srcDataByte, sampleRate);
                                    break;
                            }
                            //byte[] output = fast ? core.StereoToRGB24Fast(srcDataByte) : core.StereoToRGB24(srcDataByte);


                            srcDataByte = null;
                            LinearAccessByteImageUnsignedNonVectorized image = new LinearAccessByteImageUnsignedNonVectorized(output, videoReferenceFrame);
                            Bitmap imgBitmap = Helpers.ByteArrayToBitmap(image);
                            image = null;
                            bool addingSucceeded = false;
                            while (!addingSucceeded)
                            {
                                addingSucceeded = writeBuffer.TryAdd(index, imgBitmap); 
                                System.Threading.Thread.Sleep(mainLoopTimeout);
                            }
                            //writeBuffer.Add(index, imgBitmap);
                            imgBitmap = null;
                            imagesProcessing[index] = false;
                            imagesProcessed[index] = true;
                        });
                    });

                    UInt64 frameCount;
#if !DEBUG
                    try
                    {
#endif
                        using (SuperWAV wavFile = new SuperWAV(ofd.FileName))
                        {

                            sampleRate = wavFile.getWavInfo().sampleRate;
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

                            //byte[] srcDataByte;
                            //byte[] output;
                            //Bitmap imgBitmap; 
                            float[] srcData;
                            //LinearAccessByteImageUnsignedNonVectorized image;

                            //int currentFrame = 0;

                            //int frameToWrite = 0;
                            UInt64 imagesLeft = frameCount;
                            imagesProcessed = new bool[frameCount];
                            imagesProcessing = new bool[frameCount];

                            Int64 lastFrameWrittenIntoVideo = -1;

                            Bitmap tmpBitmap;


                            while (imagesLeft > 0)
                            {
                                int numberOfThreadsCurrentlyProcessing = 0;
                                foreach(bool imageProcessing in imagesProcessing)
                                {
                                    if (imageProcessing)
                                    {
                                        numberOfThreadsCurrentlyProcessing++;
                                    }
                                }

                                int writeBufferFillStatusWorstCase = writeBuffer.Count+numberOfThreadsCurrentlyProcessing;

                                int numberOfThreadsToSpawn = Math.Min(threads - numberOfThreadsCurrentlyProcessing,bufferSize - writeBufferFillStatusWorstCase);

                                for(int i = 0; i < numberOfThreadsToSpawn; i++)
                                {
                                    for(UInt64 a = 0; a < frameCount; a++)
                                    {
                                        if (!imagesProcessed[a] && !imagesProcessing[a]) {
                                            imagesProcessing[a] = true;

                                            srcData = wavFile.getAs32BitFloatFast(imageLength * a, imageLength * (a + 1) - 1);

                                            Task.Run(convertWorker(srcData,(int)a,fast));
                                            srcData = null;
                                            break;
                                        }
                                    }
                                }

                                // Do actual video writing
                                bool framesAvailable = true;
                                while (framesAvailable && lastFrameWrittenIntoVideo < (Int64)(frameCount-1))
                                {
                                    UInt64 nextFrameToBeWritten = (UInt64)(lastFrameWrittenIntoVideo + 1);
                                    if (imagesProcessed[nextFrameToBeWritten] && writeBuffer.ContainsKey((int)nextFrameToBeWritten))
                                    {
                                        bool readingSucceeded = writeBuffer.TryRemove((int)nextFrameToBeWritten,out tmpBitmap); // We're using remove here because that returns a bitmap whether we want to or not anyway...
                                        if (!readingSucceeded)
                                        {
                                            framesAvailable = false;
                                        } else
                                        {
                                            writer.WriteVideoFrame(tmpBitmap, (uint)nextFrameToBeWritten);
                                            tmpBitmap.Dispose();
                                            tmpBitmap = null;
                                            lastFrameWrittenIntoVideo++;
                                            imagesLeft--;
                                            writer.Flush();
                                        }

                                    } else
                                    {
                                        framesAvailable = false;
                                    }
                                }

                                System.Threading.Thread.Sleep(mainLoopTimeout);
                            }
                            /*for (UInt64 i = 0; i < frameCount; i++)
                            {
                                //srcData = wavFile.getAs32BitFloat(imageLength*i, imageLength*(i+1)-1); 
                                srcData = wavFile.getAs32BitFloatFast(imageLength*i, imageLength*(i+1)-1); 
                                


                                writer.WriteVideoFrame(imgBitmap,(uint)i);
                                writer.Flush();
                                /*if (currentFrame % 1000 == 0)
                                {
                                    progress.Report("Saving video: " + i + "/" + frameCount + " frames");
                                }*/
                            //}*/

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

        private void btnW64ToVideoFast_Click(object sender, RoutedEventArgs e)
        {
            w64ToVideo(true);
        }

        private void btnWavToImageFast_Click(object sender, RoutedEventArgs e)
        {
            wavToImage(true);
        }

        private void btnW64ToVideoFastAsync_Click(object sender, RoutedEventArgs e)
        {
            w64ToVideoMultiThreadedAsync(true);
        }

        private void btnImageToWavV2_Click(object sender, RoutedEventArgs e)
        {
            imageToWav(SinusLabCore.FormatVersion.V2);
        }

        private void btnWavToImageV2_Click(object sender, RoutedEventArgs e)
        {
            wavToImage(false,SinusLabCore.FormatVersion.V2);
        }

        private void btnWavToImageV2NoLFLuma_Click(object sender, RoutedEventArgs e)
        {
            wavToImage(false, SinusLabCore.FormatVersion.V2_NOLUMA_DECODE_ONLY);
        }

        private void btnWavToImageV2Fast_Click(object sender, RoutedEventArgs e)
        {
            wavToImage(true, SinusLabCore.FormatVersion.V2);
        }

        private void btnWavToImageV2FastNoFLLuma_Click(object sender, RoutedEventArgs e)
        {
            wavToImage(true, SinusLabCore.FormatVersion.V2_NOLUMA_DECODE_ONLY);
        }

        private void btnVideoToW64V2_Click(object sender, RoutedEventArgs e)
        {

            videoToW64(SinusLabCore.FormatVersion.V2);
        }

        private void btnW64ToVideoFastAsyncV2_Click(object sender, RoutedEventArgs e)
        {
            w64ToVideoMultiThreadedAsync(true,SinusLabCore.FormatVersion.V2);
        }

        private void btnW64ToVideoFastAsyncV2NoLuma_Click(object sender, RoutedEventArgs e)
        {
            w64ToVideoMultiThreadedAsync(true, SinusLabCore.FormatVersion.V2_NOLUMA_DECODE_ONLY);
        }

        private void btnWavToImageV2FastUltraQuality_Click(object sender, RoutedEventArgs e)
        {
            wavToImage(true, SinusLabCore.FormatVersion.V2,true);
        }




        private void updateSettings()
        {
            if (!isInitialized)
            {
                return; // If the window hasn't fully finished loading yet, don't try to read elements or it might cause an exception or unpredictable errors
            }

            core.decodeGainMultiplier = Math.Pow(2,gainSlider.Value);
            core.decodeLumaGainMultiplier = Math.Pow(2,gainSlider_Luma.Value);
            core.decodeChromaGainMultiplier = Math.Pow(2,gainSlider_Chroma.Value);
            core.decodeLFLumaGainMultiplier = Math.Pow(2,gainSlider_LFLuma.Value);
            core.windowFunction = (SinusLabCore.WindowFunction)cmbFFTWindowFunction.SelectedItem;
            int tmpSampleRate;
            bool success = int.TryParse(txtEncodeAndRawSampleRate.Text, out tmpSampleRate);
            if (success)
            {
                if(tmpSampleRate > 40000)
                {
                    if(tmpSampleRate <= (2 * core.AudioSubcarrierFrequencyV3))
                    {
                        txtSampleRateError.Text = "A sample rate of "+tmpSampleRate+" is not high enough to allow for an audio carrier in V3 encoding, if you care about that. Pick something over "+(core.AudioSubcarrierFrequencyV3 * 2);
                    } else
                    {

                        txtSampleRateError.Text = "";
                    }
                    core.samplerate = tmpSampleRate;
                } else
                {
                    // No message bc annoying af.
                    txtSampleRateError.Text = "Cannot set a sample rate below 40 kHz because chroma is encoded to 20kHz and Nyquist said so. If you want to just run it slower, convert to 48k and then reinterpret the output file as something lower, and then reverse it again.";
                    //MessageBox.Show("Cannot set a sample rate below 40 kHz because chroma is encoded to 20kHz and Nyquist said so. If you want to just run it slower, convert to 48k and then reinterpret the output file as something lower, and then reverse it again.");
                }
            }
            int tmpMaxCores;
            success = int.TryParse(txtMaxThreads.Text, out tmpMaxCores);
            if (success)
            {
                if(tmpMaxCores >0)
                {

                    maxThreads = (uint)tmpMaxCores;
                    txtMaxThreadsError.Text = "";
                } else
                {
                    // No message bc annoying af.
                    txtMaxThreadsError.Text = "Max thread count must be at least 1. Obviously.";
                    //MessageBox.Show("Cannot set a sample rate below 40 kHz because chroma is encoded to 20kHz and Nyquist said so. If you want to just run it slower, convert to 48k and then reinterpret the output file as something lower, and then reverse it again.");
                }
            }
            previewDecodeLuma = checkboxPreviewLFLumaDecode.IsChecked == true;
            previewDecodeFast = checkboxPreviewFastDecode.IsChecked == true;
            previewSuperHighQuality = checkboxPreviewUHQ.IsChecked == true;
            previewFrameIndex = (UInt64)frameSlider.Value;
        }

        bool previewDecodeLuma = true;
        bool previewDecodeFast = true;
        bool previewSuperHighQuality = false;
        SinusLabCore.FormatVersion previewFormatVersion = SinusLabCore.FormatVersion.V2;
        UInt64 previewFrameIndex = 0;
        UInt64 previewFrameIndexActuallyDisplayed = 0;
        SuperWAV previewWaveSource = null;
        Task previewDrawingTask = null;
        CancellationTokenSource previewDrawingCancellationTokenSource = null;
        string previewSourceWavFileName = "";
        string previewImageSaveSuffix = "";

        private async void redrawPreview(bool fast = false, SinusLabCore.FormatVersion formatVersion = SinusLabCore.FormatVersion.DEFAULT_LEGACY)
        {
            if (previewWaveSource == null)
            {
                return;
            }
            if(previewDrawingTask != null)
            {
                if (!previewDrawingTask.IsCompleted)
                {
                    previewDrawingCancellationTokenSource.Cancel();
                    previewDrawingCancellationTokenSource = null;
                }
            }
            previewDrawingCancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = previewDrawingCancellationTokenSource.Token;

            UInt64 imageLength = (UInt64)videoReferenceFrame.width * (UInt64)videoReferenceFrame.height;
            UInt64 thisPreviewFrameIndex = previewFrameIndex;
            float[] srcData = previewWaveSource.getAs32BitFloatFast(imageLength * thisPreviewFrameIndex, imageLength * (thisPreviewFrameIndex + 1) - 1);


            bool superHighQuality = previewSuperHighQuality;
            bool thisPreviewDecodeLuma = previewDecodeLuma;

            string basePreviewImageSaveSuffix = "frame"+ thisPreviewFrameIndex + "_V2" + (thisPreviewDecodeLuma ? "" : "NoLum") + (superHighQuality ? "UHQ" : "");

            previewDrawingTask = Task.Run(()=> {

                byte[] srcDataByte = new byte[srcData.Length * 4];
                Buffer.BlockCopy(srcData, 0, srcDataByte, 0, srcDataByte.Length);
                srcData = null;

                cancellationToken.ThrowIfCancellationRequested();

                byte[] output;

                //byte[] output = fast ? core.StereoToRGB24Fast(srcDataByte) : core.StereoToRGB24(srcDataByte);
                output =  core.StereoToRGB24V2Fast(srcDataByte, previewWaveSource.getWavInfo().sampleRate, thisPreviewDecodeLuma, superHighQuality, 0.5, false, false, 8, SinusLabCore.LowFrequencyLumaCompensationMode.OFFSET,previewFormatVersion == SinusLabCore.FormatVersion.V3,  null,  cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();
                //srcDataByte = null;
                LinearAccessByteImageUnsignedNonVectorized image = new LinearAccessByteImageUnsignedNonVectorized(output, videoReferenceFrame);
                output = null;
                cancellationToken.ThrowIfCancellationRequested();

                Dispatcher.Invoke(()=> {
                    Bitmap imgBitmap = Helpers.ByteArrayToBitmap(image);
                    image = null;
                    if (cancellationToken.IsCancellationRequested)
                    {
                        imgBitmap.Dispose();
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    previewImg.Source = Helpers.BitmapToImageSource(imgBitmap);
                });
                previewFrameIndexActuallyDisplayed = thisPreviewFrameIndex;
                previewImageSaveSuffix = basePreviewImageSaveSuffix +"_sub8";
                cancellationToken.ThrowIfCancellationRequested();
                /*
                // Now for he mid quality version: (not necessary. going below 8 on the one above doesnt yield much performance gain sadly)
                output = core.StereoToRGB24V2Fast(srcDataByte, previewDecodeLuma, false, 0.5, false, false, 8, SinusLabCore.LowFrequencyLumaCompensationMode.OFFSET, null, cancellationToken);
                image = new LinearAccessByteImageUnsignedNonVectorized(output, videoReferenceFrame);
                output = null;
                Dispatcher.Invoke(() => {
                    Bitmap imgBitmap = Helpers.ByteArrayToBitmap(image);
                    image = null;
                    previewImg.Source = Helpers.BitmapToImageSource(imgBitmap);
                    imgBitmap.Dispose();
                });*/
                // Now for he high quality version:
                output = core.StereoToRGB24V2Fast(srcDataByte, previewWaveSource.getWavInfo().sampleRate, thisPreviewDecodeLuma, superHighQuality, previewDecodeFast ? 0.5 : -1, false, false, 1, SinusLabCore.LowFrequencyLumaCompensationMode.OFFSET, previewFormatVersion == SinusLabCore.FormatVersion.V3, null,  cancellationToken);
                image = new LinearAccessByteImageUnsignedNonVectorized(output, videoReferenceFrame); 
                output = null;
                cancellationToken.ThrowIfCancellationRequested();
                Dispatcher.Invoke(() => {
                    Bitmap imgBitmap = Helpers.ByteArrayToBitmap(image);
                    image = null;
                    if (cancellationToken.IsCancellationRequested)
                    {
                        imgBitmap.Dispose();
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    previewImg.Source = Helpers.BitmapToImageSource(imgBitmap);
                    imgBitmap.Dispose();
                });
                previewImageSaveSuffix = basePreviewImageSaveSuffix + "_full";

            }, cancellationToken);

        }

        private void gainSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            updateSettings();
            redrawPreview(); 
        }

        private void btnLoadVideoIntoPreviewV2_Click(object sender, RoutedEventArgs e)
        {
            loadvideoIntoPreview();
        }

        private void loadvideoIntoPreview(SinusLabCore.FormatVersion formatVersion = SinusLabCore.FormatVersion.V2)
        {
            if (videoReferenceFrame == null)
            {
                MessageBox.Show("No reference video loaded.");
                return;
            }

            if(previewWaveSource != null)
            {
                previewWaveSource.Dispose(); // Make sure to not leak memory or keep file handles open when opening a new wave file.
            }


            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select w64 file coresponding to the loaded reference video";

            if (ofd.ShowDialog() == true)
            {

                UInt64 frameCount;
#if !DEBUG
                try
                {
#endif
                previewWaveSource = new SuperWAV(ofd.FileName);

                previewFormatVersion = formatVersion;

                UInt64 imageLength = (UInt64)videoReferenceFrame.width * (UInt64)videoReferenceFrame.height;
                frameCount = previewWaveSource.DataLengthInTicks / (imageLength);

                previewSourceWavFileName = ofd.FileName;

                frameSlider.Maximum = frameCount - 1;
                frameSlider.Value = 0; // This will automatically call RedrawPreview()

#if !DEBUG
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message);
                }
#endif

                redrawPreview();
            }
        }

        private void checkboxPreviewLFLumaDecode_Checked(object sender, RoutedEventArgs e)
        {
            updateSettings();
            redrawPreview();
        }

        private void checkboxPreviewLFLumaDecode_Unchecked(object sender, RoutedEventArgs e)
        {
            updateSettings();
            redrawPreview();
        }

        private void frameSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            updateSettings();
            redrawPreview();
        }

        private void checkboxPreviewUHQ_Checked(object sender, RoutedEventArgs e)
        {

            updateSettings();
            redrawPreview();
        }

        private void checkboxPreviewUHQ_Unchecked(object sender, RoutedEventArgs e)
        {

            updateSettings();
            redrawPreview();
        }

        private void btnW64ToVideoFastAsyncUHQV2_Click(object sender, RoutedEventArgs e)
        {
            w64ToVideoMultiThreadedAsync(true, SinusLabCore.FormatVersion.V2,true);
        }

        private void btnSavePreviewFrame_Click(object sender, RoutedEventArgs e)
        {
            savePreviewFrame();
        }
        private void savePreviewFrame()
        {
            using (Bitmap imgToSave = Helpers.ConvertToBitmap((BitmapSource)previewImg.Source))
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.FileName = previewSourceWavFileName + "_" + previewImageSaveSuffix + ".png";
                if (sfd.ShowDialog() == true)
                {
                    imgToSave.Save(sfd.FileName);
                }
            }
        }

        private void txtEncodeAndRawSampleRate_TextChanged(object sender, TextChangedEventArgs e)
        {
            updateSettings();
        }

        private void btnW64ToVideoV2_Click(object sender, RoutedEventArgs e)
        {
            w64ToVideoMultiThreadedAsync(false, SinusLabCore.FormatVersion.V2);
        }

        private void checkboxPreviewFastDecode_Checked(object sender, RoutedEventArgs e)
        {

            updateSettings();
            redrawPreview();
        }

        private void checkboxPreviewFastDecode_Unchecked(object sender, RoutedEventArgs e)
        {

            updateSettings();
            redrawPreview();
        }

        private void txtMaxThreads_TextChanged(object sender, TextChangedEventArgs e)
        {

            updateSettings();
        }

        private void btnW64ToVideoAsyncUHQV2_Click(object sender, RoutedEventArgs e)
        {
            w64ToVideoMultiThreadedAsync(false, SinusLabCore.FormatVersion.V2,true);
        }

        private void cmbFFTWindowFunction_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            updateSettings();
            redrawPreview();
        }

        private void btnVideoToW64V3_Click(object sender, RoutedEventArgs e)
        {

            videoToW64(SinusLabCore.FormatVersion.V3);
        }

        private void btnLoadVideoIntoPreviewV3_Click(object sender, RoutedEventArgs e)
        {

            loadvideoIntoPreview(SinusLabCore.FormatVersion.V3);
        }
    }
}
