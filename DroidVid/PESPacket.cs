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
using Java.IO;
using Android.Util;

namespace DroidVid
{
    public class PESPacket
    {
        private static String TAG = typeof(PESPacket).ToString();

        int id;
        public int length { get; private set; }

        bool priority;
        bool dai;
        bool copyright;
        bool origOrCopy;
        bool hasPts;
        bool hasDts;
        bool hasEscr;
        bool hasEsRate;
        bool dsmtmf;
        bool acif;
        bool hasCrc;
        bool pesef;
        int headerDataLength;

        byte[] headerData;
        internal ByteArrayOutputStream data = new ByteArrayOutputStream();

        public PESPacket(TSPacket ts)
        {
            if (ts == null || !ts.pus)
            {
                Log.Error(TAG, "invalid ts passed in");
                throw new Java.Security.InvalidParameterException("invalid ts passed in");
            }

            var pes = Java.Nio.ByteBuffer.Wrap(ts.payload);

            // start code
            if (pes.Get() != 0 || pes.Get() != 0 || pes.Get() != 1)
            {
                Log.Error(TAG, "invalid start code");
                throw new Java.Security.InvalidParameterException("invalid start code");
            }

            // stream id
            id = pes.Get() & 0xff;

            // packet length
            length = pes.GetShort(pes.Position()) & 0xffff;

            // this is supposedly allowed for video
            if (length == 0)
            {
                Log.Warn(TAG, "got zero-length PES?");
            }

            if (id != 0xe0)
            {
                Log.Error(TAG, String.Format("unexpected stream id: 0x{0:X}", id));
                // todo: ?
            }

            // for 0xe0 there is an extension header starting with 2 bits '10'
            byte b = (byte)pes.Get();
            if ((b & 0x30) != 0)
            {
                Log.Warn(TAG, "scrambled ?!?!");
                // todo: ?
            }

            var flags = Java.Util.BitSet.ValueOf(new byte[] { b });
            priority = flags.Get(3);
            dai = flags.Get(2);
            copyright = flags.Get(1);
            origOrCopy = flags.Get(0);

            flags = Java.Util.BitSet.ValueOf(new byte[] { (byte)pes.Get() });
            hasPts = flags.Get(7);
            hasDts = flags.Get(6);
            hasEscr = flags.Get(5);
            hasEsRate = flags.Get(4);
            dsmtmf = flags.Get(3);
            acif = flags.Get(2);
            hasCrc = flags.Get(1);
            pesef = flags.Get(0);

            headerDataLength = pes.Get() & 0xff;

            if (headerDataLength > 0)
            {
                headerData = new byte[headerDataLength];
                pes.Get(headerData);
            }

            //note sure what this is!
            //WritableByteChannel channel = Channels.newChannel(data);
            //try
            //{
            //    channel.write(pes);
            //}
            //catch (IOException e)
            //{
            //    //e.printStackTrace();
            //}

            // length includes optional pes header,
            length = length - (3 + headerDataLength);
        }

        public void Add(TSPacket ts)
        {
            if (ts.pus)
            {
                Log.Error(TAG, "don't add start of PES packet to another packet");
                throw new Java.Security.InvalidParameterException("ts packet marked as new pes");
            }

            int size = data.Size();
            int len = length - size;
            len = ts.payload.Length > len ? len : ts.payload.Length;
            data.Write(ts.payload, 0, len);
        }

        public bool isFull()
        {
            return (data.Size() >= length);
        }

        public long getPts()
        {
            if (!hasPts || headerDataLength < 5)
                return 0;

            var hd = Java.Nio.ByteBuffer.Wrap(headerData);
            long pts = (((hd.Get() & 0x0e) << 29)
                        | ((hd.Get() & 0xff) << 22)
                        | ((hd.Get() & 0xfe) << 14)
                        | ((hd.Get() & 0xff) << 7)
                        | ((hd.Get() & 0xfe) >> 1));

            return pts;
        }
    }
}