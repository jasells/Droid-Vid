using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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
    public abstract class Player
    {
        //these all need to go into a base class.
        protected MediaCodec decoder;
        protected MediaFormat format;
        protected BufferExtractor buffEx;
        protected Android.Media.MediaCodec.BufferInfo info;
        protected System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

        public volatile bool running;
        protected Java.Nio.ByteBuffer[] inputBuffers;

        public Task DecodeThread { get; private set; }
        public volatile bool interrupted = false;

        protected Surface surface;
        //protected Context myContext;

        protected Player(Surface surf) { surface = surf; }

        /// <summary>
        /// starts a task to decode the video
        /// </summary>
        /// <returns></returns>
        public Task RunAsync()
        {
            //var DecodeThread = new Task(() => Run(), TaskCreationOptions.LongRunning);
            //DecodeThread.Start();
            //return DecodeThread;

            return (DecodeThread = Task.Run(() => Run()));
        }

        public abstract void Run();

        protected static void PrintFormatInfo(MediaFormat format)
        {
            var csd0 = format.GetByteBuffer("csd-0");
            var csd1 = format.GetByteBuffer("csd-1");

            if (csd0 == null || csd1 == null)
                return;

            Log.Debug("csd0 buff len: ", csd0.Limit().ToString());
            Log.Debug("csd0 buff pos: ", csd0.Position().ToString());

            var sOut = "{ ";

            byte[] buff0 = new byte[csd0.Limit()];
            //int buffi = 0;
            csd0.Get(buff0, 0, buff0.Length);

            sOut = "{";
            foreach (byte b in buff0)
                sOut += " "+ b + ", ";

            sOut = sOut.TrimEnd(",".ToCharArray()) + "}";

            Log.Debug("csd-0[] data: ", sOut);

            //while (csd0.HasRemaining)
            //{
            //    buff0[buffi] = csd0.Get();

            //    Log.Debug("csd-0[" + buffi + "] data: ",
            //                string.Format("0x{0:X}", buff0[buffi]));

            //    ++buffi;
            //}
            csd0.Position(0);
            Log.Debug("csd1 buff len: ", csd0.Limit().ToString());
            Log.Debug("csd1 buff pos: ", csd0.Position().ToString());

            byte[] buff1 = new byte[csd1.Limit()];
            //buffi = 0;

            csd1.Get(buff1, 0, buff1.Length);

            sOut = "{";
            foreach (byte b in buff1)
                sOut += " "+b + ",";

            sOut = sOut.TrimEnd(",".ToCharArray()) + "}";

            Log.Debug("csd-1[] data: ", sOut);
            //while (csd1.HasRemaining)
            //{
            //    buff1[buffi] = csd1.Get();

            //    Log.Debug("csd-1[] data: ",
            //                string.Format("0x{0:X}", buff1[buffi]));

            //    ++buffi;
            //}
            csd1.Position(0);
        }
    }

    public class FilePlayer :Player
    {
        public static string dir = "/Removable/MicroSD/";//"/mnt/shared/extSdCard/";//
        public static string SAMPLE = dir + "Video_2014_5_6__16_51_56.mp4";//dir+"Video_2014_5_6__16_51_56.mp4";//


        public FilePlayer(Surface surface):base(surface)
        {
            //myContext = context;
        }

        
        override public async void Run()
        {
            //Android.Media.MediaExtractor extractor;

            //Android.Media.MediaCodec decoder = null;

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
                //var mf = new MediaFormat();
                //mf.SetString(MediaFormat.KeyMime, "video/avc");
                //mf.SetInteger(MediaFormat.KeyMaxInputSize, 144154);// 1024 * 100);
                //mf.SetInteger(MediaFormat.KeyWidth, 720);
                //mf.SetInteger(MediaFormat.KeyHeight, 480);
                //mf.SetInteger("max-width", 720);
                //mf.SetInteger("max-height", 480);
                ////mf.SetInteger("push-blank-buffers-on-shudown", 1);

                //Log.Debug("Format info: ", mf.ToString());

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
                        decoder = Android.Media.MediaCodec.CreateDecoderByType(mime);
                        //this is where the Xamarin Android VM dies.
                        decoder.Configure(format, surface, null, 0);
                        break;
                    }
                }

                if (decoder == null)
                {
                    Android.Util.Log.Error("DecodeActivity", "Can't find video info!");
                    return;//can't continue...
                }

                using (decoder)
                {
                    decoder.Start();

                    var inputBuffers = decoder.GetInputBuffers();
                    var outputBuffers = decoder.GetOutputBuffers();
                    var info = new Android.Media.MediaCodec.BufferInfo();
                    bool isEOS = false;
                    var sw = new System.Diagnostics.Stopwatch();
                    long startMs = sw.ElapsedMilliseconds;
                    sw.Start();
                    byte[] peekBuf = new byte[188];


                    while (!interrupted)
                    {
                        //sw.Restart();

                        if (!isEOS)
                        {
                            int inIndex = decoder.DequeueInputBuffer(10000);
                            if (inIndex >= 0)
                            {
                                var buffer = inputBuffers[inIndex];// decoder.GetInputBuffer(inIndex);
                                if(buffer.Position() != 0)
                                    Log.Debug("inBuff.Position: ", buffer.Position().ToString());
                                Log.Debug("inBuff: ", buffer.ToString());

                                int sampleSize = extractor.ReadSampleData(buffer, buffer.Position());
                                if (sampleSize < 0)
                                {
                                    // We shouldn't stop the playback at this point, just pass the EOS
                                    // flag to decoder, we will get it again from the
                                    // dequeueOutputBuffer
                                    Log.Debug("DecodeActivity", MediaCodecBufferFlags.EndOfStream.ToString());
                                    decoder.QueueInputBuffer(inIndex, 0, 0, 0, MediaCodecBufferFlags.EndOfStream);
                                    isEOS = true;
                                }
                                else
                                {
                                    if(peekBuf.Length < sampleSize)
                                        peekBuf = new byte[sampleSize];
                                    peekBuf.Initialize();//clear old data.
                                    buffer.Get(peekBuf);
                                    buffer.Position(0);//reset for the decoder

                                    for(int i =4; i<peekBuf.Length; ++i)
                                    {
                                        if (peekBuf[i] == 0x01
                                            && peekBuf[i - 1] == 0x00
                                            && peekBuf[i - 2] == 0x00
                                            && peekBuf[i - 3] == 0x00)
                                            Log.Debug("Found h264 start code: ",
                                                        string.Format("i={0} of {1}", i, sampleSize));
                                    }

                                    Log.Debug("ExtractorActivity, sampleSize: ", sampleSize.ToString());

                                    decoder.QueueInputBuffer(inIndex, 0, sampleSize, extractor.SampleTime, 0);
                                    //await extractor.AdvanceAsync().ConfigureAwait(false);
                                   extractor.AdvanceAsync();
                                }
                            }
                        }

                        int outIndex = decoder.DequeueOutputBuffer(info, 10000);
                        switch ((Android.Media.MediaCodecInfoState)outIndex)
                        {
                            case MediaCodecInfoState.OutputBuffersChanged:
                                Android.Util.Log.Debug("DecodeActivity",
                                                        MediaCodecInfoState.OutputBuffersChanged.ToString());
                                outputBuffers = decoder.GetOutputBuffers();
                                break;
                            case MediaCodecInfoState.OutputFormatChanged:
                                PrintFormatInfo(decoder.OutputFormat);

                                Android.Util.Log.Debug("DecodeActivity", "New format " + decoder.OutputFormat);//.GetOutputFormat(outIndex));



                                break;
                            case MediaCodecInfoState.TryAgainLater:
                                Android.Util.Log.Debug("DecodeActivity", "dequeueOutputBuffer timed out!");
                                break;
                            default:
                                var buffer = outputBuffers[outIndex];// decoder.GetOutputBuffer(outIndex);
                                Android.Util.Log.Verbose("DecodeActivity", "We can't use this buffer but render it due to the API limit, " + buffer);

                                // We use a very simple clock to keep the video FPS, or the video
                                // playback will be too fast
                                //This causes the next frame to not be rendered too quickly.
                                while (info.PresentationTimeUs / 1000 > sw.ElapsedMilliseconds - startMs)
                                {
                                    try
                                    {
                                        await Task.Delay(10).ConfigureAwait(false);
                                        //sleep(10);
                                    }
                                    catch (Exception e)
                                    {
                                        //e.printStackTrace();
                                        System.Diagnostics.Debug.WriteLine(e.StackTrace);
                                        break;
                                    }
                                }
                                //the decoder won't advance without this...
                                //must be called before the next decoder.dequeue call
                                decoder.ReleaseOutputBuffer(outIndex, true);
                                break;
                        }

                        // All decoded frames have been rendered, we can stop playing now
                        if ((info.Flags & MediaCodecBufferFlags.EndOfStream) != 0)
                        {
                            Android.Util.Log.Debug("DecodeActivity",
                                                    MediaCodecBufferFlags.EndOfStream.ToString());
                            break;
                        }
                    }

                    decoder.Stop();
                }
            }
        }

        
    }
}