using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using System.Threading.Tasks;
using Android.Media;
using Android.Util;
using MpegTS;

namespace DroidVid
{
    public class FilePlayer2 :FilePlayer
    {
        private static string TAG = typeof(FilePlayer2).ToString();
        //public static string dir = "/Removable/MicroSD/";//"/mnt/shared/extSdCard/";//
        //public static string SAMPLE = dir + "Video_2014_5_6__16_51_56.mp4";//dir+"Video_2014_5_6__16_51_56.mp4";//
        const int formatStartVal = 518013543;
        const int formatStartI = 64;
        public volatile bool running;

        public FilePlayer2(Surface surface):base(surface)
        {
        }


        override public async void Run()
        {
            //Android.Media.MediaExtractor extractor;

            Android.Media.MediaCodec decoder = null;

            using (var extractor = new Android.Media.MediaExtractor())
            //using (Android.Media.MediaCodec decoder = null)
            {
                //extractor = new Android.Media.MediaExtractor();
                try
                {
                    await extractor.SetDataSourceAsync(SAMPLE).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    var s = ex.ToString();
                    return;
                }

                for (int i = 0; i < extractor.TrackCount; i++)
                {
                    var format = extractor.GetTrackFormat(i);

                    Log.Debug("Format info: ", format.ToString());

                    String mime = format.GetString(Android.Media.MediaFormat.KeyMime);
                    if (mime.StartsWith("video/"))
                    {
                        Log.Debug("Format mime: ", mime);
                        //Log.Debug("Format " + MediaFormat.KeyMaxInputSize + ": ",
                        //            format.GetInteger(MediaFormat.KeyMaxInputSize).ToString());
                        Log.Debug("Format " + MediaFormat.KeyWidth + ": ",
                                    format.GetInteger(MediaFormat.KeyWidth).ToString());
                        Log.Debug("Format " + MediaFormat.KeyHeight + ": ",
                                    format.GetInteger(MediaFormat.KeyHeight).ToString());

                        PrintFormatInfo(format);

                        extractor.SelectTrack(i);
                        //decoder = Android.Media.MediaCodec.CreateDecoderByType(mime);
                        //this is where the Xamarin Android VM dies.
                        //decoder.Configure(format, surface, null, 0);
                        break;
                    }
                }

                //if (decoder == null)
                //{
                //    Android.Util.Log.Error("DecodeActivity", "Can't find video info!");
                //    return;//can't continue...
                //}
                var f = new Java.IO.File(dir+"decode.out");
                if (f.Exists())
                    f.Delete();

                f.CreateNewFile();

                var f2 = new Java.IO.File(dir + "decode2.out");
                if (f2.Exists())
                    f2.Delete();

                f2.CreateNewFile();

                //open the file for our custom extractor
                var inInfo = new System.IO.FileInfo(SAMPLE);
                if (!inInfo.Exists)
                {
                    Log.Error("input file not found!", inInfo.FullName);
                    return;
                }

                using (var inStream = inInfo.OpenRead())
                using (var fs2 = new Java.IO.FileOutputStream(f2))//get an output stream
                using (var fs = new Java.IO.FileOutputStream(f))//get an output stream
                {

                    //var inputBuffers = decoder.GetInputBuffers();
                    //var outputBuffers = decoder.GetOutputBuffers();
                    var info = new Android.Media.MediaCodec.BufferInfo();
                    bool started = false, isEOS = false;
                    var sw = new System.Diagnostics.Stopwatch();
                    long startMs = sw.ElapsedMilliseconds;
                    sw.Start();
                    byte[] peekBuf = new byte[188];
                    //for dumping the sample into instead of the decoder.
                    var buffer = Java.Nio.ByteBuffer.Allocate(165000);// decoder.GetInputBuffer(inIndex);
                    var buffEx = new BufferExtractor();
                    var tmpB = new byte[20000];


                    while (!interrupted)
                    {
                        //sw.Restart();

                        if (!isEOS)
                        {
                            int inIndex = 1;// decoder.DequeueInputBuffer(10000);
                            if (inIndex >= 0)
                            {
                                buffer.Position(0);//reset the buffer
                                if (buffer.Position() != 0)
                                    Log.Debug("inBuff.Position: ", buffer.Position().ToString());
                                Log.Debug("inBuff: ", buffer.ToString());

                                int sampleSize = extractor.ReadSampleData(buffer, 0);
                                if (sampleSize < 0)
                                {
                                    // We shouldn't stop the playback at this point, just pass the EOS
                                    // flag to decoder, we will get it again from the
                                    // dequeueOutputBuffer
                                    Log.Debug("DecodeActivity", MediaCodecBufferFlags.EndOfStream.ToString());
                                    //decoder.QueueInputBuffer(inIndex, 0, 0, 0, MediaCodecBufferFlags.EndOfStream);
                                    isEOS = true;
                                }
                                else
                                {
                                    if (peekBuf.Length < sampleSize)
                                        peekBuf = new byte[sampleSize];
                                    peekBuf.Initialize();//clear old data.
                                    buffer.Get(peekBuf);
                                    buffer.Position(0);//reset for the decoder

                                    for (int i = 4; i < peekBuf.Length; ++i)
                                    {
                                        if (peekBuf[i] == 0x01
                                            && peekBuf[i - 1] == 0x00
                                            && peekBuf[i - 2] == 0x00
                                            && peekBuf[i - 3] == 0x00)
                                            Log.Debug("Found h264 start code: ",
                                                        string.Format("i={0} of {1}", i, sampleSize));
                                    }

                                    Log.Debug("ExtractorActivity, sampleSize: ", sampleSize.ToString());

                                    if (!started)//get your parser synced with theirs
                                    {
                                        do
                                        {
                                            peekBuf = new byte[188];
                                            await inStream.ReadAsync(peekBuf, 0, peekBuf.Length)
                                                            .ConfigureAwait(false);

                                            buffEx.AddRaw(peekBuf);

                                            if (buffEx.outBuffers.Count > 0
                                                && buffEx.outBuffers.Peek().GetPayload().Length != sampleSize)
                                            {
                                                buffEx.outBuffers.Dequeue();//throw this one away
                                            }

                                        } while (buffEx.outBuffers.Count == 0);
                                        started = true;
                                    }
                                    else
                                    {
                                        do
                                        {
                                            peekBuf = new byte[188];
                                            await inStream.ReadAsync(peekBuf, 0, peekBuf.Length)
                                                            .ConfigureAwait(false);

                                            buffEx.AddRaw(peekBuf);

                                        } while (buffEx.outBuffers.Count == 0);
                                        started = true;
                                    }

                                    //write out the vid data.
                                    buffer.Limit(sampleSize);
                                    buffer.Position(0);
                                    //if (tmpB.Length < sampleSize)
                                        tmpB = new byte[sampleSize];

                                    buffer.Get(tmpB);
                                    fs.Write(tmpB);

                                    buffer.Limit(buffer.Capacity());//reset the limit for next sample
                                    buffer.Position(0);

                                    fs2.Write(buffEx.outBuffers.Dequeue().GetPayload());

                                    if (!inStream.CanRead)
                                        isEOS = true;//end of stream.

                                    //decoder.QueueInputBuffer(inIndex, 0, sampleSize, extractor.SampleTime, 0);
                                    await extractor.AdvanceAsync().ConfigureAwait(false);
                                    //extractor.AdvanceAsync();
                                }
                            }
                        }


                        // All decoded frames have been rendered, we can stop playing now
                        if ((info.Flags & MediaCodecBufferFlags.EndOfStream) != 0)
                        {
                            Android.Util.Log.Debug("DecodeActivity",
                                                    MediaCodecBufferFlags.EndOfStream.ToString());
                            break;
                        }
                    }

                    //decoder.Stop();
                }
            }
        }
    }
}