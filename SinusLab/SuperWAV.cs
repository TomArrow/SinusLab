using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SinusLab
{
    class SuperWAV
    {

        public enum WavFormat
        {
            UNDEFINED_INVALID,
            WAVE,
            WAVE64,
            RF64
        }

        WavFormat wavFormat = WavFormat.UNDEFINED_INVALID;
        bool writingAllowed = false;
        FileStream fs;
        BinaryReader br;
        BinaryWriter bw;

        byte[] WAVE64_GUIDFOURCC_RIFF_LAST12 = new byte[12] {0x2e, 0x91, 0xcf, 0x11, 0xa5, 0xd6, 0x28, 0xdb, 0x04, 0xc1, 0x00, 0x00 };
        byte[] WAVE64_GUIDFOURCC_LAST12 = new byte[12] {0xf3, 0xac, 0xd3, 0x11, 0x8c, 0xd1, 0x00, 0xc0, 0x4f, 0x8e, 0xdb, 0x8a };
        const UInt64 WAVE64_SIZE_DIFFERENCE = 24; // This is the size of the 128 bit fourcc code and the 64 bit size field that are part of the size parameter itself in Wave64
        UInt32 RF64_MINUS1_VALUE = BitConverter.ToUInt32(new byte[4] { 0xFF,0xFF,0xFF,0xFF},0);

        struct ChunkInfo
        {
            public string name;
            public UInt64 size;
            public bool isValidWave64LegacyRIFFCode;
        }

        public enum AudioFormat
        {
            LPCM = 1,
            FLOAT=3,
            RF64_FLOAT= 65534 // I'm not 100% confident about this one. It works, but I'm not sure why RF64 doesn't just use the normal value for FLOAT. Maybe an error that ffmpeg makes?
        }

        public struct WavInfo
        {
            public UInt32 sampleRate;
            public UInt16 channelCount;
            public AudioFormat audioFormat; // We only support uncompressed = 1 for now
            public UInt32 byteRate;
            public UInt16 bitsPerSample;
            public UInt16 bytesPerTick;
            public UInt64 dataOffset;
            public UInt64 dataLength;
        }

        WavInfo wavInfo;

        // Helper variables to speed up things
        UInt16 bytesPerSample;
        UInt64 dataLengthInTicks;

        public enum OpenMode
        {
            OPEN_FOR_READ,
            CREATE_FOR_READ_WRITE,
            CREATE_OR_OPEN_FOR_READ_WRITE
        }

        OpenMode openMode = OpenMode.OPEN_FOR_READ;

        // Constructor for reading
        public SuperWAV(string path)
        {
            openMode = OpenMode.OPEN_FOR_READ;


            fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            br = new BinaryReader(fs);

            wavFormat = detectWavFormat();

            if (wavFormat != WavFormat.WAVE && wavFormat != WavFormat.WAVE64 && wavFormat != WavFormat.RF64)
            {
                throw new Exception("Only normal WAV and WAVE64 and RF64 is supported so far, not anything else.");
            }

            wavInfo = readWavInfo();

            if (wavInfo.audioFormat != AudioFormat.LPCM && wavInfo.audioFormat != AudioFormat.FLOAT)
            {
                throw new Exception("Only uncompressed WAV currently supported.");
            }

            // Sanity checks
            if (wavInfo.bitsPerSample * wavInfo.channelCount / 8 != wavInfo.bytesPerTick)
            {
                throw new Exception("Uhm what?");
            }
            else if (wavInfo.byteRate != wavInfo.sampleRate * wavInfo.bytesPerTick)
            {
                throw new Exception("Uhm what?");
            }

            bytesPerSample = (UInt16)(wavInfo.bitsPerSample / 8U);
            dataLengthInTicks = wavInfo.dataLength / wavInfo.bytesPerTick;
            
        }

        // Constructor for writing
        public SuperWAV(string path, WavFormat wavFormatForWritingA, UInt32 sampleRateA, UInt16 channelCountA, AudioFormat audioFormatA, UInt16 bitsPerSampleA,UInt64 initialDataLengthInTicks = 0)
        {
            openMode = OpenMode.CREATE_FOR_READ_WRITE;

            fs = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
            br = new BinaryReader(fs);
            bw = new BinaryWriter(fs);

            bytesPerSample = (UInt16)( bitsPerSampleA / 8);
            dataLengthInTicks = initialDataLengthInTicks;

            wavInfo.sampleRate = sampleRateA;
            wavInfo.channelCount = channelCountA;
            wavInfo.audioFormat = audioFormatA;
            wavInfo.bitsPerSample = bitsPerSampleA;
            wavInfo.bytesPerTick = (UInt16)(bytesPerSample * channelCountA);
            wavInfo.dataLength = initialDataLengthInTicks* wavInfo.bytesPerTick;
            wavInfo.byteRate = wavInfo.sampleRate * wavInfo.bytesPerTick;

            wavFormat = wavFormatForWritingA;

            writeFileHusk(wavFormatForWritingA,ref wavInfo);

        }

        // Increase size of data chunk if necessary.
        public void checkAndIncreaseDataSize(UInt64 requiredDataSizeInTicks)
        {
            if(openMode == OpenMode.OPEN_FOR_READ)
            {
                throw new Exception("Trying to manipulate file that was opened for reading only!");
            } else if (openMode == OpenMode.CREATE_OR_OPEN_FOR_READ_WRITE)
            {
                throw new Exception("Modifying existing files is not yet implemented.");
            } else if (openMode == OpenMode.CREATE_FOR_READ_WRITE)
            {
                if(wavFormat == WavFormat.WAVE)
                {
                    if (requiredDataSizeInTicks > dataLengthInTicks)
                    {
                        wavInfo.dataLength = requiredDataSizeInTicks * wavInfo.bytesPerTick;

                        if(wavInfo.dataLength > UInt32.MaxValue)
                        {
                            throw new Exception("Trying to allocate more than 4GB of data in traditional wav file.");
                        }

                        bw.BaseStream.Seek((Int64)wavInfo.dataOffset, SeekOrigin.Begin);
                        bw.BaseStream.Seek((Int64)wavInfo.dataLength - 1,SeekOrigin.Current);
                        bw.Write((byte)0);
                        Int64 currentPosition = bw.BaseStream.Position;
                        bw.Seek(4, SeekOrigin.Begin);
                        bw.Write((UInt32)currentPosition-8);  // Check if -8 is actually correct
                        bw.BaseStream.Seek((Int64)wavInfo.dataOffset-(Int64)4, SeekOrigin.Begin);
                        bw.Write((UInt32)wavInfo.dataLength);
                        dataLengthInTicks = requiredDataSizeInTicks;
                    }
                }
                else if (wavFormat == WavFormat.WAVE64)
                {
                    if (requiredDataSizeInTicks > dataLengthInTicks)
                    {
                        wavInfo.dataLength = requiredDataSizeInTicks * wavInfo.bytesPerTick;

                        bw.BaseStream.Seek((Int64)wavInfo.dataOffset, SeekOrigin.Begin);
                        bw.BaseStream.Seek((Int64)wavInfo.dataLength - 1, SeekOrigin.Current);
                        bw.Write((byte)0);
                        Int64 currentPosition = bw.BaseStream.Position;
                        bw.Seek(4+12, SeekOrigin.Begin);
                        bw.Write((UInt64)currentPosition);
                        bw.BaseStream.Seek((Int64)wavInfo.dataOffset - (Int64)8, SeekOrigin.Begin);
                        bw.Write((UInt64)wavInfo.dataLength + WAVE64_SIZE_DIFFERENCE);
                    }
                }
                else if (wavFormat == WavFormat.RF64)
                {
                    throw new Exception("Writing RF64 is not yet implemented.");
                }
                else
                {
                    throw new Exception("Whut? " + wavFormat + "? What do you mean by " + wavFormat + "?");
                }

            }
        }

        // Write the bare minimum for a working file.
        private void writeFileHusk(WavFormat wavFormatA,ref WavInfo wavInfoA)
        {
            if(openMode == OpenMode.CREATE_FOR_READ_WRITE) { 

                if(wavFormatA == WavFormat.WAVE)
                {
                    bw.Seek(0,SeekOrigin.Begin);
                    bw.Write("RIFF".ToCharArray());
                    bw.Write((UInt32)0);
                    bw.Write("WAVE".ToCharArray());
                    bw.Write("fmt ".ToCharArray());
                    bw.Write((UInt32)16);
                    bw.Write((UInt16)wavInfoA.audioFormat);
                    bw.Write((UInt16)wavInfoA.channelCount);
                    bw.Write((UInt32)wavInfoA.sampleRate);
                    bw.Write((UInt32)wavInfoA.byteRate);
                    bw.Write((UInt16)wavInfoA.bytesPerTick);
                    bw.Write((UInt16)wavInfoA.bitsPerSample);
                    bw.Write("data".ToCharArray());
                    bw.Write((UInt32)wavInfoA.dataLength);
                    wavInfoA.dataOffset = (UInt64)bw.BaseStream.Position;
                    bw.BaseStream.Seek((Int64)wavInfoA.dataLength-1, SeekOrigin.Current);
                    bw.Write((byte)0);
                    Int64 currentPosition = bw.BaseStream.Position;
                    bw.Seek(4, SeekOrigin.Begin);
                    bw.Write((UInt32)currentPosition-8); // Check if -8 is actually correct



                } else if(wavFormat == WavFormat.WAVE64)
                {


                    bw.Seek(0, SeekOrigin.Begin);
                    bw.Write("riff".ToCharArray());
                    bw.Write(WAVE64_GUIDFOURCC_RIFF_LAST12);
                    bw.Write((UInt64)0);
                    bw.Write("wave".ToCharArray());
                    bw.Write(WAVE64_GUIDFOURCC_LAST12);
                    bw.Write("fmt ".ToCharArray());
                    bw.Write(WAVE64_GUIDFOURCC_LAST12);
                    bw.Write((UInt64)(16+ WAVE64_SIZE_DIFFERENCE));
                    bw.Write((UInt16)wavInfoA.audioFormat);
                    bw.Write((UInt16)wavInfoA.channelCount);
                    bw.Write((UInt32)wavInfoA.sampleRate);
                    bw.Write((UInt32)wavInfoA.byteRate);
                    bw.Write((UInt16)wavInfoA.bytesPerTick);
                    bw.Write((UInt16)wavInfoA.bitsPerSample);
                    bw.Write("data".ToCharArray());
                    bw.Write(WAVE64_GUIDFOURCC_LAST12);
                    bw.Write((UInt64)wavInfoA.dataLength + WAVE64_SIZE_DIFFERENCE);
                    wavInfoA.dataOffset = (UInt64)bw.BaseStream.Position;
                    bw.BaseStream.Seek((Int64)wavInfoA.dataLength - 1, SeekOrigin.Current);
                    bw.Write((byte)0);
                    Int64 currentPosition = bw.BaseStream.Position;
                    bw.Seek(4+12, SeekOrigin.Begin);
                    bw.Write((UInt64)currentPosition);
                } else if(wavFormat == WavFormat.RF64)
                {
                    throw new Exception("Writing RF64 is not yet implemented.");
                } else
                {
                    throw new Exception("Whut? "+wavFormat+"? What do you mean by "+wavFormat+"?");
                }
            } else
            {
                throw new Exception("Trying to initialize an already existing file! Don't do that!");
            }
        }

        // TODO Optimize this more and find out how I can return by ref
        public float[] getEntireFileAs32BitFloat()
        {
            float[] retVal = new float[wavInfo.channelCount*dataLengthInTicks];
            double[] tmp;
            for (UInt64 i=0; i<dataLengthInTicks;i++)
            {
                tmp = this[i];
                for(uint c = 0; c < wavInfo.channelCount; c++)
                {

                    retVal[i*wavInfo.channelCount+c] = (float)tmp[c];
                }
            }
            return retVal;
        }

        public void writeFloatArray(float [] dataToAdd, UInt64 offset=0)
        {
            UInt64 dataToAddLengthInTicks = (UInt64)dataToAdd.Length / (UInt64)wavInfo.channelCount;
            double[] tmp = new double[wavInfo.channelCount];
            for (UInt64 i = 0; i < dataToAddLengthInTicks; i++)
            {
                for (uint c = 0; c < wavInfo.channelCount; c++)
                {
                    tmp[c] = dataToAdd[i * wavInfo.channelCount + c];
                }
                this[i] = tmp;
            }
        }

        public WavInfo getWavInfo()
        {
            return wavInfo;
        }


        // test: (UInt32)(((double)(UInt32.MaxValue - 2)/ (double)UInt32.MaxValue)*(double)UInt32.MaxValue)
        public double[] this[UInt64 index]
        {
            get
            {
                double[] retVal = new double[wavInfo.channelCount];

                UInt64 baseOffset = wavInfo.dataOffset + index * wavInfo.bytesPerTick;
                byte[] readBuffer;
                br.BaseStream.Seek((Int64)baseOffset, SeekOrigin.Begin);

                readBuffer = br.ReadBytes(wavInfo.bytesPerTick);

                switch (wavInfo.bitsPerSample)
                {
                    case 8:
                        for(int i = 0; i < wavInfo.channelCount; i++)
                        {
                            retVal[i] = (double)((double)readBuffer[i] - 128.0)/Math.Abs((double)sbyte.MinValue);
                        }
                        break;
                    case 16:
                        Int16[] tmp0 = new Int16[wavInfo.channelCount];
                        Buffer.BlockCopy(readBuffer,0,tmp0,0,wavInfo.bytesPerTick);
                        for (int i = 0; i < wavInfo.channelCount; i++)
                        {
                            retVal[i] = (double)((double)tmp0[i] / Math.Abs((double)Int16.MinValue));
                        }
                        break;
                    case 32:
                        if(wavInfo.audioFormat == AudioFormat.FLOAT)
                        {
                            float[] tmp1 = new float[wavInfo.channelCount];
                            Buffer.BlockCopy(readBuffer, 0, tmp1, 0, wavInfo.bytesPerTick);
                            for (int i = 0; i < wavInfo.channelCount; i++)
                            {
                                retVal[i] = (double)tmp1[i];
                            }
                        } else
                        {
                            Int32[] tmp2 = new Int32[wavInfo.channelCount];
                            Buffer.BlockCopy(readBuffer, 0, tmp2, 0, wavInfo.bytesPerTick);
                            for (int i = 0; i < wavInfo.channelCount; i++)
                            {
                                retVal[i] = (double)((double)tmp2[i] / Math.Abs((double)Int32.MinValue));
                            }
                        }
                        break;
                    // Test:
                    // Int16[] abc = new Int16[1]{Int16.MaxValue};Int32[] hah = new Int32[1]{0}; Buffer.BlockCopy(abc,0,hah,0,2); hah[0] // bad
                    // Int16[] abc = new Int16[1]{Int16.MinValue};Int32[] hah = new Int32[1]{0}; Buffer.BlockCopy(abc,0,hah,0,2); hah[0] // bad
                    // Int16[] abc = new Int16[1]{Int16.MaxValue};Int32[] hah = new Int32[1]{0}; Buffer.BlockCopy(abc,0,hah,2,2); hah[0] //correctly scaled
                    // Int16[] abc = new Int16[1]{Int16.MinValue};Int32[] hah = new Int32[1]{0}; Buffer.BlockCopy(abc,0,hah,2,2); hah[0] //correctly scaled
                    case 24: // Untested
                        Int32[] singleOne = new Int32[1] { 0 }; // We just interpret as Int32 and ignore one byte.
                        for (int i = 0; i < wavInfo.channelCount; i++)
                        {
                            Buffer.BlockCopy(readBuffer, i*3, singleOne, 1, 3);
                            retVal[i] = (double)((double)singleOne[0] / Math.Abs((double)Int32.MinValue));
                        }
                        break;
                }
                /*for (uint i = 0; i < wavInfo.channelCount; i++)
                {
                    offset = baseOffset + i * bytesPerSample;
                    readBuffer = br.ReadBytes(bytesPerSample);
                    
                }*/

                return retVal;
            }

            set
            {

                if(value.Length != wavInfo.channelCount)
                {
                    throw new Exception("Data array supplied for writing does not match channel count.");
                }
                if (openMode == OpenMode.OPEN_FOR_READ)
                {
                    throw new Exception("Trying to edit file that was opened for reading only!");
                }
                else if (openMode == OpenMode.CREATE_OR_OPEN_FOR_READ_WRITE)
                {
                    throw new Exception("Modifying existing files is not yet implemented.");
                }
                else if (openMode == OpenMode.CREATE_FOR_READ_WRITE)
                {
                    UInt64 startOffset = index * wavInfo.bytesPerTick;
                    //UInt64 endOffset = startOffset + wavInfo.bytesPerTick -1;
                    checkAndIncreaseDataSize(index);
                    UInt64 startOffsetAbsolute = wavInfo.dataOffset+ startOffset;

                    byte[] dataToWrite = new byte[wavInfo.bytesPerTick];

                    switch (wavInfo.bitsPerSample)
                    {
                        case 8:
                            for(int i = 0; i < wavInfo.channelCount; i++)
                            {
                                dataToWrite[i] = (byte)(Math.Min(sbyte.MaxValue, Math.Max(sbyte.MinValue, value[i] * Math.Abs((double)sbyte.MinValue))) + 128.0);
                            }
                            break;
                        case 16:
                            Int16[] tmp0 = new Int16[wavInfo.channelCount];
                            for (int i = 0; i < wavInfo.channelCount; i++)
                            {
                                tmp0[i] = (Int16)(Math.Min(Int16.MaxValue, Math.Max(Int16.MinValue, value[i] * Math.Abs((double)Int16.MinValue))));
                            }
                            Buffer.BlockCopy(tmp0,0,dataToWrite,0,dataToWrite.Length);
                            break;
                        case 32:
                            if (wavInfo.audioFormat == AudioFormat.FLOAT)
                            {
                                float[] tmp1 = new float[wavInfo.channelCount];
                                for (int i = 0; i < wavInfo.channelCount; i++)
                                {
                                    tmp1[i] = (float)value[i];
                                }
                                Buffer.BlockCopy(tmp1, 0, dataToWrite, 0, dataToWrite.Length);
                            }
                            else
                            {
                                Int32[] tmp2 = new Int32[wavInfo.channelCount];
                                for (int i = 0; i < wavInfo.channelCount; i++)
                                {
                                    tmp2[i] = (Int32)(Math.Min(Int32.MaxValue, Math.Max(Int32.MinValue, value[i] * Math.Abs((double)Int32.MinValue))));
                                }
                                Buffer.BlockCopy(tmp2, 0, dataToWrite, 0, dataToWrite.Length);
                            }
                            break;
                        case 24:
                            Int32[] tmp3 = new Int32[1];
                            for (int i = 0; i < wavInfo.channelCount; i++)
                            {
                                tmp3[0] = (Int32)(Math.Min(Int32.MaxValue, Math.Max(Int32.MinValue, value[i] * Math.Abs((double)Int32.MinValue))));
                                Buffer.BlockCopy(tmp3, 1, dataToWrite, i*3, 3);
                            }
                            break;
                    }

                    bw.BaseStream.Seek((Int64)startOffsetAbsolute,SeekOrigin.Begin);
                    bw.Write(dataToWrite);
                }
            }
        }


        private WavFormat detectWavFormat()
        {

            ChunkInfo chunk = readChunk32(0);
            if(chunk.name == "RIFF")
            {
                // Either Wave64 or normal WAV
                chunk = readChunk32(12);
                if(chunk.name == "FMT " && chunk.size == 16)
                {
                    // Probably normal wav?
                    return WavFormat.WAVE;
                } else 
                {
                    chunk = readChunkWave64(40);
                    if (chunk.name == "FMT " && chunk.size == 16 && chunk.isValidWave64LegacyRIFFCode)
                    {
                        // Probably wave64? But need to properly read specification to make sure. Just based on hexeditor.
                        return WavFormat.WAVE64;
                    }
                }
            } else if(chunk.name == "RF64")
            {
                chunk = readChunk32(12);
                if (chunk.name == "DS64")
                {
                    // RF64
                    return WavFormat.RF64;
                }
            }
            
            // If nothing else returns something valid, we failed at detecting.
            return WavFormat.UNDEFINED_INVALID;
        }

        private WavInfo readWavInfo()
        {
            WavInfo retVal = new WavInfo();
            if(wavFormat == WavFormat.WAVE)
            {
                

                // find fmt chunk
                ChunkInfo chunk = new ChunkInfo();
                UInt64 currentPosition = 12;
                UInt64 resultPosition;
                do
                {
                    chunk = readChunk32(currentPosition); // TODO gracefully handle error if no data chunk exists. Currently would crash.
                    resultPosition = currentPosition;
                    currentPosition += 8 + chunk.size;

                } while (chunk.name != "FMT ");

                br.BaseStream.Seek((Int64)(resultPosition + (UInt64)8), SeekOrigin.Begin);


                retVal.audioFormat = (AudioFormat)br.ReadUInt16();
                retVal.channelCount = br.ReadUInt16();
                retVal.sampleRate = br.ReadUInt32();
                retVal.byteRate = br.ReadUInt32();
                retVal.bytesPerTick = br.ReadUInt16();
                retVal.bitsPerSample = br.ReadUInt16();


                // find data chunk
                currentPosition = 12;
                do
                {
                    chunk = readChunk32(currentPosition); // TODO gracefully handle error if no data chunk exists. Currently would crash.
                    resultPosition = currentPosition;
                    currentPosition += 8 + chunk.size;

                } while (chunk.name != "DATA");

                retVal.dataOffset = resultPosition + 8;
                retVal.dataLength = chunk.size;

            } else if (wavFormat == WavFormat.WAVE64) // Todo: respect 8 byte boundaries.
            {
                // find fmt chunk
                ChunkInfo chunk = new ChunkInfo();
                UInt64 currentPosition = 40;
                UInt64 resultPosition;
                do
                {
                    chunk = readChunkWave64(currentPosition); // TODO gracefully handle error if no data chunk exists. Currently would crash.
                    resultPosition = currentPosition;
                    currentPosition += 24 + chunk.size;

                } while (chunk.name != "FMT " || !chunk.isValidWave64LegacyRIFFCode);

                br.BaseStream.Seek((Int64)(resultPosition + (UInt64)24), SeekOrigin.Begin);

                //br.BaseStream.Seek(64, SeekOrigin.Begin);
                retVal.audioFormat = (AudioFormat)br.ReadUInt16();
                retVal.channelCount = br.ReadUInt16();
                retVal.sampleRate = br.ReadUInt32();
                retVal.byteRate = br.ReadUInt32();
                retVal.bytesPerTick = br.ReadUInt16();
                retVal.bitsPerSample = br.ReadUInt16();


                // find data chunk
                currentPosition = 40;
                do
                {
                    chunk = readChunkWave64(currentPosition); // TODO gracefully handle error if no data chunk exists. Currently would crash.
                    resultPosition = currentPosition;
                    currentPosition += 24 + chunk.size;

                } while (chunk.name != "DATA" || !chunk.isValidWave64LegacyRIFFCode);

                retVal.dataOffset = resultPosition + 24;
                retVal.dataLength = chunk.size;
            }
            else if (wavFormat == WavFormat.RF64)
            {
                br.BaseStream.Seek(20, SeekOrigin.Begin);

                UInt64 ds64_riffSize = br.ReadUInt64();
                UInt64 ds64_dataSize = br.ReadUInt64();

                // find fmt chunk
                ChunkInfo chunk = new ChunkInfo();
                UInt64 currentPosition = 12;
                UInt64 resultPosition;
                do
                {
                    chunk = readChunk32(currentPosition); // TODO gracefully handle error if no data chunk exists. Currently would crash.
                    resultPosition = currentPosition;
                    currentPosition += 8 + chunk.size;

                } while (chunk.name != "FMT ");

                br.BaseStream.Seek((Int64)(resultPosition+(UInt64)8), SeekOrigin.Begin);

                // read fmt chunk data, as usual
                retVal.audioFormat = (AudioFormat)br.ReadUInt16();
                retVal.channelCount = br.ReadUInt16();
                retVal.sampleRate = br.ReadUInt32();
                retVal.byteRate = br.ReadUInt32();
                retVal.bytesPerTick = br.ReadUInt16();
                retVal.bitsPerSample = br.ReadUInt16();

                if(retVal.audioFormat == AudioFormat.RF64_FLOAT) // I'm not 100% confident about this one. It works, but I'm not sure why RF64 doesn't just use the normal value for FLOAT. Maybe an error that ffmpeg makes?
                {
                    retVal.audioFormat = AudioFormat.FLOAT;
                }

                // find data chunk
                currentPosition = 12;
                do
                {
                    chunk = readChunk32(currentPosition); // TODO gracefully handle error if no data chunk exists. Currently would crash.
                    resultPosition = currentPosition;
                    currentPosition += 8 + chunk.size;

                } while (chunk.name != "DATA");

                retVal.dataOffset = resultPosition + 8;
                retVal.dataLength = chunk.size == RF64_MINUS1_VALUE ? ds64_dataSize : chunk.size; // According to specification, we must read the size from this chunk unless it's FFFFFFFF (or -1 if interpreted as signed Int32)

            }
            else
            {
                // Not supported (yet)
            }
            return retVal;
        }

        private ChunkInfo readChunk32(UInt64 position)
        {
            br.BaseStream.Seek((Int64)position,SeekOrigin.Begin);
            ChunkInfo retVal = new ChunkInfo();
            byte[] nameBytes = br.ReadBytes(4);
            retVal.name = Encoding.ASCII.GetString(nameBytes).ToUpper();
            retVal.size = br.ReadUInt32();
            return retVal;
        }
        private ChunkInfo readChunkWave64(UInt64 position)
        {
            br.BaseStream.Seek((Int64)position,SeekOrigin.Begin);
            ChunkInfo retVal = new ChunkInfo();
            byte[] nameBytes = br.ReadBytes(4);
            byte[] fourCC = br.ReadBytes(12);
            retVal.isValidWave64LegacyRIFFCode = Helpers.EqualBytesLongUnrolled(fourCC, WAVE64_GUIDFOURCC_LAST12);
            retVal.name = Encoding.ASCII.GetString(nameBytes).ToUpper();
            retVal.size = br.ReadUInt64()-(UInt64)24U;
            return retVal;
        }


        ~SuperWAV()
        {
            if(openMode == OpenMode.OPEN_FOR_READ)
            {

                br.Dispose();
                fs.Close();
            } else
            {

                br.Dispose();
                bw.Dispose();
                fs.Close();
            }
        }
    }
}
