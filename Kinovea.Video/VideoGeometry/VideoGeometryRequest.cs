using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kinovea.Services;

namespace Kinovea.Video
{
    /// <summary>
    /// Describes what the player is requesting in terms of image geometry.
    /// These are all the parameters that directly impact the reader.
    /// </summary>
    public class VideoGeometryRequest
    {
        /// <summary>
        /// Size of the rectangle where the image is going to be rendered.
        /// This may be empty initially.
        /// This may be larger or smaller than the viewport itself.
        /// </summary>
        public Size PresentationSize { get; }

        /// <summary>
        /// Scale factor applied to the reference size to produce the presentation size.
        /// </summary>
        public double PresentationScale { get; }
        /// <summary>
        /// Scale factor applied to the reference size to fit into the viewport.
        /// </summary>
        public double ViewportFitScale { get; }

        /// <summary>
        /// Whether the player allows the reader to pre-scale the image.
        /// For example while we are tracking or enumerating frames for export
        /// we disallow any prescaling.
        /// </summary>
        public bool AllowPreScaling { get; }

        public ImageAspectRatio AspectRatio { get; }
        
        public ImageRotation Rotation { get; }

        public Demosaicing Demosaicing { get; }

        public bool Deinterlace { get; }

        public List<TimedPoint> StabilizationData { get; }

        public VideoGeometryRequest(
            Size presentationSize, 
            double presentationScale,
            double viewportFitScale,
            bool allowPreScaling, 
            ImageAspectRatio aspectRatio,
            ImageRotation rotation,
            Demosaicing demosaicing,
            bool deinterlace,
            List<TimedPoint> stabilizationData)
        {
            PresentationSize = presentationSize;
            PresentationScale = presentationScale;
            ViewportFitScale = viewportFitScale;
            AllowPreScaling = allowPreScaling;
            AspectRatio = aspectRatio;
            Rotation = rotation;
            Demosaicing = demosaicing;
            Deinterlace = deinterlace;
            StabilizationData = stabilizationData;
        }
    }
}
