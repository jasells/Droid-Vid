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

        private string myAddress;
        private int myPort;

        public SocketPlayer(Surface surf, int port)
            :base(surf)
        {
            //myAddress = address;
            myPort = port;

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

        override protected async void Run()
        {
            //var sock = new Sockets.Plugin.UdpSocketReceiver();
            var sock = new System.Net.Sockets.UdpClient(
                            new System.Net.IPEndPoint(System.Net.IPAddress.Any, myPort));
            sock.EnableBroadcast = true;

            using (sock.Client)//make sure the socket gets cleaned up on exit
            using (decoder)
            {
                //config the decoder with the info/meta data from the stream
                decoder.Configure(format, surface, null, MediaCodecConfigFlags.None);
                decoder.Start();

                bool isEOS = false;
                bool foundNAL = false;
                int count = 0;

                while (running)
                {
                    ++count;
                    try
                    {
                        while (buffEx.SampleCount == 0 && running)
                        {
                            //we need a new buffer every loop!
                            //buff = new byte[188];
                            var pack = await sock.ReceiveAsync().ConfigureAwait(false);

                            //we may need to check the source here too? or specify above.
                            if (pack.Buffer.Length != TsPacket.PacketLength )
                                continue;//wait for next packet, not a TS packet

                            //could check # of non-TS packets and abort if too many?

                            //push the raw data to our custom extractor
                            if (!buffEx.AddRaw(pack.Buffer))
                            {
                                Log.Debug("ExtractorActivity,   ", " ----------bad TS packet!");

                            }
                        }

                        if (!running)
                            break;//exit the render loop
                    }
                    catch (Exception ex)
                    {
                        Log.Error("ExtractorActivity error: ", ex.ToString());
                    }

                    var sample = buffEx.DequeueNextSample();
                    Log.Debug("ExtractorActivity, sampleSize: ", sample.Length.ToString());

                    //get a input buffer index from the decoder for input
                    int inIndex = decoder.DequeueInputBuffer(10000);


                    if (inIndex >= 0)
                    {
                        //get the re-assembled video data from the extractor
                        using (var b = Java.Nio.ByteBuffer.Wrap(sample.Buffer))
                        {

                            var inB = inputBuffers[inIndex];
                            //*************
                            //THE BUFFER *******MUST********* be CLEARED before each write,
                            //else when the buffers start getting recycled, the decoder will
                            //read past the end of the current data into old data!
                            //This may cause tearing of the picture, or even a complete 
                            //crash of the app from internal errors in native decoder code!!!!!
                            inB.Clear();
                            inB.Put(b);//put data into the decoder's native buffer

                            //tell the decoder about the new data in the buffer
                            decoder.QueueInputBuffer(inIndex, 0, b.Limit(), 0, MediaCodecBufferFlags.None);

                        }//  b.Dispose();//clean up
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

                    //no clock needed for live streaming?
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
            }

            info = null;
            decoder = null;
        }
    }
}