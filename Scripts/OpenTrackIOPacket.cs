using System;
using System.Text;
using UnityEngine;

namespace OpenTrackIO
{
    [Serializable]
    public class Packet
    {
        public Static @static;
        public Timing timing;
        public Lens lens;
        public Protocol protocol;
        public string sourceId;
        public int sourceNumber;
        public TransformItem[] transforms;
        public Custom custom;

        [Serializable]
        public class Static
        {
            public Camera camera;
            public Lens lens;
        }

        [Serializable]
        public class Camera
        {
            public Rational captureFrameRate;
            public Dimensions activeSensorPhysicalDimensions;
            public Resolution activeSensorResolution;
            public string make;
            public string model;
            public string serialNumber;
            public string firmwareVersion;
            public string label;
            public int isoSpeed;
            public float shutterAngle;
        }

        [Serializable]
        public class Lens
        {
            public string make;
            public string model;
            public string serialNumber;
            public string firmwareVersion;
            public float nominalFocalLength;
            public float[] custom;
            public Distortion[] distortion;
            public Offset distortionOffset;
            public Encoders encoders;
            public float entrancePupilOffset;
            public float fStop;
            public float pinholeFocalLength;
            public float focusDistance;
            public Offset projectionOffset;
            public RawEncoders rawEncoders;
        }

        [Serializable]
        public class Timing
        {
            public Rational sampleRate;
            public Timecode timecode;
        }

        [Serializable]
        public class Protocol
        {
            public string name;
            public int[] version;
        }

        [Serializable]
        public class TransformItem
        {
            public string id;
            public Rotation rotation;
            public Translation translation;
        }

        [Serializable]
        public class Custom
        {
            public MetaData[] MetaData;
            public int pot1;
            public bool button1;
        }

        [Serializable]
        public class Rational
        {
            public int num;
            public int denom;
        }

        [Serializable]
        public class Dimensions
        {
            public float height;
            public float width;
        }

        [Serializable]
        public class Resolution
        {
            public int height;
            public int width;
        }

        [Serializable]
        public class Timecode
        {
            public int hours;
            public int minutes;
            public int seconds;
            public int frames;
            public Rational frameRate;
            public int subFrame;
        }

        [Serializable]
        public class Distortion
        {
            public string model;
            public float[] radial;
            public float[] tangential;
            public float[] custom;
            public float overscan;
        }

        [Serializable]
        public class Offset
        {
            public float x;
            public float y;
        }

        [Serializable]
        public class Rotation
        {
            public float pan;
            public float tilt;
            public float roll;
        }

        [Serializable]
        public class Translation
        {
            public float x;
            public float y;
            public float z;
        }

        [Serializable]
        public class Encoders
        {
            public float focus;
            public float iris;
            public float zoom;
        }

        [Serializable]
        public class RawEncoders
        {
            public int focus;
            public int iris;
            public int zoom;
        }

        [Serializable]
        public class MetaData
        {
            public string key;
            public string value;
        }

        internal class OpenTrackIOHeader
        {
            public string identifier;
            public byte reserved;
            public byte encoding;
            public ushort sequenceNumber;
            public uint segmentOffset;
            public bool lastSegmentFlag;
            public int payloadLength;
            public ushort checksum;
        }

        public static Packet Decode(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("OpenTrackIO packet is empty.");
            }

#if UNITY_EDITOR || DEBUG
            Debug.Log($"Received raw data ({data.Length} bytes): {BitConverter.ToString(data)}");
#endif

            var header = ParseHeader(data);
            string json;

            if (header != null && header.identifier == "OTrk")
            {
                if (header.encoding != 0x01)
                {
                    throw new NotSupportedException($"OpenTrackIO encoding {header.encoding} is not supported.");
                }

                if (data.Length < 16 + header.payloadLength)
                {
                    throw new ArgumentException("OpenTrackIO packet is shorter than expected payload length.");
                }

                json = ExtractJsonPayload(data, header);
            }
            else
            {
                json = ExtractJsonPayload(data, null);
            }

            if (string.IsNullOrEmpty(json))
            {
                throw new Exception("Failed to extract JSON payload from OpenTrackIO packet.");
            }

            try
            {
                var packet = JsonUtility.FromJson<Packet>(json);
                if (packet == null)
                {
                    throw new Exception("Failed to parse OpenTrackIO JSON payload.");
                }
                return packet;
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"JSON parse error: {e.Message}. JSON: {json}");
                throw;
            }
        }

        internal static OpenTrackIOHeader ParseHeader(byte[] data)
        {
            if (data.Length < 16)
            {
                return null;
            }

            var header = new OpenTrackIOHeader
            {
                identifier = Encoding.ASCII.GetString(data, 0, 4),
                reserved = data[4],
                encoding = data[5],
                sequenceNumber = (ushort)((data[6] << 8) | data[7]),
                segmentOffset = (uint)((data[8] << 24) | (data[9] << 16) | (data[10] << 8) | data[11])
            };

            ushort lengthField = (ushort)((data[12] << 8) | data[13]);
            header.lastSegmentFlag = (lengthField & 0x8000) != 0;
            header.payloadLength = lengthField & 0x7FFF;
            header.checksum = (ushort)((data[14] << 8) | data[15]);

            return header;
        }

        private static string ExtractJsonPayload(byte[] data, OpenTrackIOHeader header)
        {
            string json;
            if (header != null && header.identifier == "OTrk")
            {
                json = Encoding.UTF8.GetString(data, 16, header.payloadLength);
#if UNITY_EDITOR || DEBUG
                Debug.Log($"OpenTrackIO header detected. Identifier={header.identifier}, payload length={header.payloadLength}. Extracted JSON payload: {json}");
#endif
            }
            else
            {
                json = Encoding.UTF8.GetString(data);
#if UNITY_EDITOR || DEBUG
                Debug.Log($"No OpenTrackIO header detected. Treating full packet as JSON-like payload. Header identifier: {header?.identifier ?? "<none>"}");
                Debug.Log($"Raw payload prefix: {BitConverter.ToString(data, 0, Math.Min(data.Length, 32))}");
#endif
            }
            return json.Trim();
        }
    }
}

