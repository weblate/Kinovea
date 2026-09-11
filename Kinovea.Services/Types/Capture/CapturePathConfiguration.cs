using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace Kinovea.Services
{
    public class CapturePathConfiguration
    {
        /// <summary>
        /// List of capture folders defined by the user.
        /// </summary>
        public List<CaptureFolder> CaptureFolders { get; set; }
        /// <summary>
        /// Default file name.
        /// May contain variables.
        /// </summary>
        public string DefaultFileName { get; set; }
        /// <summary>
        /// Image format to use when capturing images.
        /// </summary>
        public KinoveaImageFormat ImageFormat { get; set; }
        /// <summary>
        /// Video format to use when capturing MJPEG videos.
        /// </summary>
        public VideoContainer VideoFormat { get; set; }

        /// <summary>
        /// Codec to use when capturing videos (MJPEG/RAW).
        /// </summary>
        public CaptureCodec CaptureCodec { get; set; }

        /// <summary>
        /// Quality to use when capturing videos (for MJPEG).
        /// </summary>
        public EncodingQuality EncodingQuality { get; set; }

        
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        
        public CapturePathConfiguration()
        {
            // Default configuration.

            string root = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            root = Path.Combine(root, "Capture");

            CaptureFolder captureA = new CaptureFolder
            {
                Id = Guid.NewGuid(),
                ShortName = "Capture A",
                Path = Path.Combine(root, "Capture A")
            };

            CaptureFolder captureB = new CaptureFolder
            {
                Id = Guid.NewGuid(),
                ShortName = "Capture B",
                Path = Path.Combine(root, "Capture B")
            };

            CaptureFolders = new List<CaptureFolder> { captureA, captureB };
            DefaultFileName = "%dateb%-%time%";
            ImageFormat = KinoveaImageFormat.JPG;
            VideoFormat = VideoContainer.MP4;
            CaptureCodec = CaptureCodec.MJPEG;
            EncodingQuality = EncodingQuality.Good;
        }

        public CapturePathConfiguration Clone()
        {
            CapturePathConfiguration cloned = new CapturePathConfiguration();

            cloned.CaptureFolders.Clear();
            foreach (CaptureFolder folder in this.CaptureFolders)
                cloned.CaptureFolders.Add(folder.Clone());
            cloned.DefaultFileName = this.DefaultFileName;
            cloned.ImageFormat = this.ImageFormat;
            cloned.VideoFormat = this.VideoFormat;
            cloned.CaptureCodec = this.CaptureCodec;
            cloned.EncodingQuality = this.EncodingQuality;
            return cloned;
        }


        #region Serialization
        public void ReadXml(XmlReader r)
        {
            r.ReadStartElement();

            while (r.NodeType == XmlNodeType.Element)
            {
                switch (r.Name)
                {
                    case "CaptureFolders":
                        ParseCaptureFolders(r);
                        break;
                    case "DefaultFileName":
                        DefaultFileName = r.ReadElementContentAsString();
                        break;
                    case "ImageFormat":
                        ImageFormat = (KinoveaImageFormat)Enum.Parse(typeof(KinoveaImageFormat), r.ReadElementContentAsString());
                        break;
                    case "VideoFormat":
                        VideoFormat = (VideoContainer)Enum.Parse(typeof(VideoContainer), r.ReadElementContentAsString());
                        break;
                    case "CaptureCodec":
                        CaptureCodec = (CaptureCodec)Enum.Parse(typeof(CaptureCodec), r.ReadElementContentAsString());
                        break;
                    case "EncodingQuality":
                        EncodingQuality = (EncodingQuality)Enum.Parse(typeof(EncodingQuality), r.ReadElementContentAsString());
                        break;
                    default:
                        string outerXml = r.ReadOuterXml();
                        log.DebugFormat("Unparsed content in XML: {0}", outerXml);
                        break;
                }
            }

            r.ReadEndElement();
        }

        private void ParseCaptureFolders(XmlReader r)
        {
            CaptureFolders.Clear();
            bool empty = r.IsEmptyElement;

            r.ReadStartElement();

            if (empty)
                return;

            while (r.NodeType == XmlNodeType.Element)
            {
                if (r.Name == "CaptureFolder")
                {
                    CaptureFolder folder = new CaptureFolder(r);
                    CaptureFolders.Add(folder);
                }
                else
                {
                    r.ReadOuterXml();
                }
            }

            r.ReadEndElement();
        }

        public void WriteXml(XmlWriter w)
        {
            if (CaptureFolders.Count > 0)
            {
                w.WriteStartElement("CaptureFolders");
                foreach (CaptureFolder folder in CaptureFolders)
                {
                    w.WriteStartElement("CaptureFolder");
                    folder.WriteXML(w);
                    w.WriteEndElement();
                }
                w.WriteEndElement();
            }

            w.WriteElementString("DefaultFileName", DefaultFileName);
            w.WriteElementString("ImageFormat", ImageFormat.ToString());
            w.WriteElementString("VideoFormat", VideoFormat.ToString());
            w.WriteElementString("CaptureCodec", CaptureCodec.ToString());
            w.WriteElementString("EncodingQuality", EncodingQuality.ToString());
        }
        #endregion
    }
}
