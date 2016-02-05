using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MpegTS
{
    /// <summary>
    /// <see cref="https://en.wikipedia.org/wiki/MPEG_transport_stream"/>
    /// 
    /// </summary>
    public class AdaptationField
    {

        public bool Discontinuity { get { return (data[5] & 0x80) > 0; } }
        /// <summary>
        /// Random Access Indicator
        /// </summary>
        public bool RandomAccess { get { return (data[5] & 0x40) > 0; } }
        /// <summary>
        /// Elementary stream priority indicator
        /// </summary>
        public bool ESPI { get { return (data[5] & 0x20) > 0; } }
        public bool HasPCR { get { return (data[5] & 0x10) > 0; } }
        public bool HasOPCR { get { return (data[5] & 0x08) > 0; } }
        public bool SplicingPointFlag { get { return (data[5] & 0x04) > 0; } }
        public bool TransportPrivData { get { return (data[5] & 0x02) > 0; } }
        public bool HasExtension { get { return (data[5] & 0x01) > 0; } }

        /// <summary>
        /// first byte after Ts header is length of this field (bytes)
        /// </summary>
        public byte Length { get { return data[4]; } }

        /// <summary>
        /// unsure if this is correct conversion!
        /// </summary>
        public ulong ProgramRefClock
        {
            get
            {
                ulong val = ((ulong)BitConverter.ToUInt32(data, 6)) << 16;
                return val & BitConverter.ToUInt16(data, 6 + 4);
            }
        } 

        private byte[] data;//from parent TsPacket

        internal AdaptationField(TsPacket parent)
        {
            data = parent.data;
        }
    }
}
