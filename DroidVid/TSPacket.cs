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
using Java.Nio;
using Android.Util;

namespace DroidVid
{
    public class TSPacket
    {
        private static String TAG = typeof(TSPacket).ToString();

        class AdaptationField
        {

            bool di;
            bool rai;
            bool espi;
            bool hasPcr;
            bool hasOpcr;
            bool spf;
            bool tpdf;
            bool hasExtension;

            byte[] data;

            public AdaptationField(ByteBuffer raw)
            {
                // first byte is size of field minus size byte
                int count = raw.Get() & 0xff;

                // second byte is flags
                var flags = Java.Util.BitSet.ValueOf(new byte[] { (byte)raw.Get() });

                di = flags.Get(7);
                rai = flags.Get(6);
                espi = flags.Get(5);
                hasPcr = flags.Get(4);
                hasOpcr = flags.Get(3);
                spf = flags.Get(2);
                tpdf = flags.Get(1);
                hasExtension = flags.Get(0);

                // the rest is 'data'
                if (count > 1)
                {
                    data = new byte[count - 1];
                    raw.Get(data);
                }
            }
        }

        bool tei;
        internal bool pus;
        bool tp;
        internal int pid;
        bool hasAdapt;
        bool hasPayload;
        int counter;
        AdaptationField adaptationField;
        internal byte[] payload;

        public TSPacket(ByteBuffer raw)
        {
            // check for sync byte
            if (raw.Get() != 0x47)
            {
                Log.Error(TAG, "missing sync byte");
                throw new Java.Security.InvalidParameterException("missing sync byte");
            }

            // next 3 bits are flags
            byte b = (byte)raw.Get();
            var flags = Java.Util.BitSet.ValueOf(new byte[] { b });

            tei = flags.Get(7);
            pus = flags.Get(6);
            tp = flags.Get(5);

            // then 13 bits for pid
            pid = ((b << 8) | (raw.Get() & 0xff)) & 0x1fff;

            b = (byte)raw.Get();
            flags = Java.Util.BitSet.ValueOf(new byte[] { b });

            // then 4 more flags
            if (flags.Get(7) || flags.Get(6))
            {
                Log.Error(TAG, "scrambled?!?!");
                // todo: bail on this packet?
            }

            hasAdapt = flags.Get(5);
            hasPayload = flags.Get(4);

            // counter
            counter = b & 0x0f;

            // optional adaptation field
            if (hasAdapt)
            {
                adaptationField = new AdaptationField(raw);
            }

            // optional payload field
            if (hasPayload)
            {
                payload = new byte[raw.Remaining()];
                raw.Get(payload);
            }
        }
    }
}