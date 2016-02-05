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
//using Debug = Android.Util.Log;


namespace DroidVid
{
    //public enum PID
    //{
    //    PAT = 0x00,
    //    PMT = 0x10,
    //    VideoStr = 0x11,
    //    KLV = 0x21,
    //    StillImage = 0x91
    //}

    //a ref I've been using:
    //http://stackoverflow.com/questions/19742047/how-to-use-mediacodec-without-mediaextractor-for-h264
    public class SocketPlayer:Player
    {
        private static string TAG = typeof(SocketPlayer).ToString();

        const int formatStartVal = 518013543;
        const int formatStartI = 64;

        public SocketPlayer(Surface surf):base(surf)
        {
            format = MediaFormat.CreateVideoFormat("video/avc", 720, 480);


            //these are custom/dependant upon the source of the video
            //**TODO: search for and find them @ runtime. (it is not hard to do), but that
            //can cause a slight delay in initial video rendering.
            var sps = new byte[] { 0, 0, 0, 1, 103, 66, 224, 30, 218, 2, 208, 246, 192, 68, 0, 0, 46, 236, 0, 10, 252, 130, 16 };
            var pps = new byte[] { 0, 0, 0, 1, 104, 206, 60, 128 };

            format.SetByteBuffer("csd-0", Java.Nio.ByteBuffer.Wrap(sps));
            format.SetByteBuffer("csd-1", Java.Nio.ByteBuffer.Wrap(pps));
            format.SetInteger(MediaFormat.KeyMaxInputSize, 720 * 480);//don't need this if you look up the pps/sps?
            Log.Debug(TAG, "format: " + format);
        }

        override public async void Run()
        {
            using (decoder)
            {
                //config the decoder with the info/meta data from the stream
                decoder.Configure(format, surface, null, MediaCodecConfigFlags.None);
                decoder.Start();

                var inputBuffers = decoder.GetInputBuffers();
                var outputBuffers = decoder.GetOutputBuffers();
                var info = new Android.Media.MediaCodec.BufferInfo();
                bool isEOS = false;
                bool foundNAL = false;
                int count = 0;
                var sw = new System.Diagnostics.Stopwatch();
                
                //sw.Start();

                int bufLen = buff.Length;
                while (!interrupted)
                {
                    //sw.Restart();

                    ++count;
                    try
                    {
                        while (fs.CanRead && buffEx.SampleCount == 0)
                        {
                            if (fs.Length - fs.Position < 188)
                            {
                                isEOS = true;
                                break;//we're @ EOF
                            }

                            //we need a new buffer every loop!
                            buff = new byte[188];
                            bytes = await fs.ReadAsync(buff, 0, buff.Length)
                                            .ConfigureAwait(false);

                            //push the raw data to our custom extractor
                            if (!buffEx.AddRaw(buff))
                            {
                                Log.Debug("ExtractorActivity,   ", " ----------bad TS packet!");

                                //find next sync byte and try again
                                fs.Position -= buff.Length
                                              - buff.ToList().IndexOf(MpegTS.TsPacket.SyncByte);
                            }
                        }

                        if (!fs.CanRead || isEOS)
                            break;//exit the render loop
                    }
                    catch (Exception ex)
                    {
                        Log.Error("ExtractorActivity error: ", ex.ToString());
                    }


                    if (sampleSize < 0)
                            {
                                // We shouldn't stop the playback at this point, just pass the EOS
                                // flag to decoder, we will get it again from the
                                // dequeueOutputBuffer

                                Log.Debug("DecodeActivity", MediaCodecBufferFlags.EndOfStream.ToString());
                                decoder.QueueInputBuffer(inIndex, 0, 0, 0, MediaCodecBufferFlags.EndOfStream);
                                isEOS = true;
                            }
                            else if(pid == PID.H264Video)
                            {
                                Log.Debug("SocketPlayer queueing data", "PID: " + string.Format("{0}", pid));

                                inBuff.Clear();//reset buff position?
                                inBuff.Put(buff, 0, sampleSize);
                                inBuff.Clear();

                                int sampleTime = 0;//no sample time provided here... maybe available, in meta data?
                                decoder.QueueInputBuffer(inIndex, 0, sampleSize, sampleTime, 0);
                                //await extractor.AdvanceAsync().ConfigureAwait(false);
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

                            Android.Util.Log.Debug("DecodeActivity", "New format " + decoder.OutputFormat);//.GetOutputFormat(outIndex));
                            break;
                        case MediaCodecInfoState.TryAgainLater:
                            Android.Util.Log.Debug("DecodeActivity", "dequeueOutputBuffer timed out!");
                            break;
                        default:
                            //must pull off old buffers to keep things going...
                            var buffer = outputBuffers[outIndex];
                            Android.Util.Log.Verbose("DecodeActivity", "We can't use this buffer but render it due to the API limit, " + buffer);

                            //// We use a very simple clock to keep the video FPS, or the video
                            //// playback will be too fast
                            //while (info.PresentationTimeUs / 1000 > sw.ElapsedMilliseconds)
                            //{
                            //    try
                            //    {
                            //        await Task.Delay(10).ConfigureAwait(false);
                            //        //sleep(10);
                            //    }
                            //    catch (Exception e)
                            //    {
                            //        //e.printStackTrace();
                            //        System.Diagnostics.Debug.WriteLine(e.StackTrace);
                            //        break;
                            //    }
                            //}
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
                //}
            }
        }
    }
}