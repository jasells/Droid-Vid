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

        public static string dir = "/Removable/MicroSD/";//"/mnt/shared/extSdCard/";//
        public static string SAMPLE = dir + "Video_2014_5_6__16_51_56.mp4";//dir+"Video_2014_5_6__16_51_56.mp4";//
        const int formatStartVal = 518013543;
        const int formatStartI = 64;

        public SocketPlayer(Surface surf):base(surf)
        {
            
        }

        override public async void Run()
        {
            Android.Media.MediaCodec decoder = null;

            var finfo = new System.IO.FileInfo(FilePlayer.SAMPLE);
            var fs = finfo.OpenRead();
            int buffSize = 188;
            var buff = new byte[buffSize];
            PID pid;

            var mf = new MediaFormat();
            mf.SetString(MediaFormat.KeyMime, "video/avc");
            mf.SetInteger(MediaFormat.KeyMaxInputSize, 1024 * 100);
            mf.SetInteger(MediaFormat.KeyWidth, 720);
            mf.SetInteger(MediaFormat.KeyHeight, 480);
            mf.SetInteger("push-blank-buffers-on-shudown", 1);

            do
            {
                int bytes = await fs.ReadAsync(buff, 0, buff.Length).ConfigureAwait(false);
                pid = (PID)buff[2];

                Log.Debug("SocketPlayer", "PID: " + string.Format("{0}", pid));

            } while (BitConverter.ToInt32(buff, formatStartI) != formatStartVal);

            var tmpB = new byte[23+8];
            Buffer.BlockCopy(buff, formatStartI - 4, tmpB, 0, tmpB.Length);

            mf.SetByteBuffer("csd-0", Java.Nio.ByteBuffer.Wrap(tmpB));

            decoder = MediaCodec.CreateDecoderByType(mf.GetString(MediaFormat.KeyMime));

            if (decoder == null)
            {
                Android.Util.Log.Error("DecodeActivity", "Can't find video info!");

                fs.Dispose();
                return;//can't continue...
            }

            var bQ = new Queue<byte[]>(270);

            using (fs)//make sure things get cleaned up
            using (decoder)
            {
                //config the decoder with the info/meta data from the stream
                decoder.Configure(mf, surface, null, 0);
                decoder.Start();

                var inputBuffers = decoder.GetInputBuffers();
                var outputBuffers = decoder.GetOutputBuffers();
                var info = new Android.Media.MediaCodec.BufferInfo();
                bool isEOS = false;
                bool foundNAL = false;
                var sw = new System.Diagnostics.Stopwatch();
                //long startMs = sw.ElapsedMilliseconds;//this is un-used
                sw.Start();

                int bufLen = buff.Length;
                while (!interrupted)
                {
                    //sw.Restart();

                    if (!isEOS)
                    {
                        int inIndex = decoder.DequeueInputBuffer(10000);
                        if (inIndex >= 0)
                        {
                            var inBuff = inputBuffers[inIndex];
                            int sampleSize = 0;
                            int tmpSamp = 0;

                            do
                            {
                                buff = new byte[buffSize];
                                tmpSamp = await fs.ReadAsync(buff, 0, bufLen).ConfigureAwait(false);
                                if (tmpSamp == 0)
                                {
                                    sampleSize = -1;//set EOS
                                    isEOS = true;
                                    break;//end loop
                                }

                                pid = (PID)buff[2];

                                //if (pid != PID.PAT && pid != PID.VideoStr)
                                //    await fs.ReadAsync(buff, 0, bufLen).ConfigureAwait(false); ;

                                switch (pid)
                                {
                                    case PID.PAT:
                                    case PID.PMT:

                                        continue;//restart the loop. look for next packet
                                        break;
                                    case PID.H264Video:
                                        //should try this jsut skipping non-video frames! duh
                                        foundNAL = (BitConverter.ToInt32(buff, formatStartI) == formatStartVal);

                                        if (!foundNAL)
                                            sampleSize += tmpSamp;
                                        else//we found the start of a new NAL in video stream
                                        {

                                        }

                                        //add the current packet, even if it is the start of
                                        //next video segment.
                                        bQ.Enqueue(buff);
                                        break;
                                }

                            } while (tmpSamp > 0 && !foundNAL);


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

                            // We use a very simple clock to keep the video FPS, or the video
                            // playback will be too fast
                            while (info.PresentationTimeUs / 1000 > sw.ElapsedMilliseconds)
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