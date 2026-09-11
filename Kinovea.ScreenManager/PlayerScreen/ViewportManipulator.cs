#region License
/*
Copyright © Joan Charmant 2012.
jcharmant@gmail.com 
 
This file is part of Kinovea.

Kinovea is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License version 2 
as published by the Free Software Foundation.

Kinovea is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with Kinovea. If not, see http://www.gnu.org/licenses/.
*/
#endregion
using System;
using System.Drawing;
using Kinovea.Video;
using Kinovea.Services;

namespace Kinovea.ScreenManager
{
    /// <summary>
    /// Compute the presentation window size and location, and the stretch factor compared to the reference.
    /// </summary>
    public class ViewportManipulator
    {
        #region Properties

        /// <summary>
        /// Size of the rendering surface inside the viewport.
        /// </summary>
        public Size RenderingSize
        {
            get { return renderingSize; }
        }

        /// <summary>
        /// Location of the rendering surface inside the viewport.
        /// </summary>
        public Point RenderingLocation
        {
            get { return renderingLocation; }
        }

        /// <summary>
        /// Final stretch factor going from reference to presentation size.
        /// </summary>
        public double Stretch
        {
            get { return stretchFactor; }
        }
        #endregion

        #region Members
        private Size renderingSize;               
        private Point renderingLocation;

        // Asked stretch factor.
        // Will be updated during the computation if it's too large to fit.
        // This is the factor applied to the reference size in order to make it fit in the drawing surface.
        private double stretchFactor = 1.0;       
        private VideoReader reader;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        #endregion

        public void Initialize(FrameServerPlayer frameServer)
        {
            this.reader = frameServer.VideoReader;
        }

        /// <summary>
        /// Compute the presentation window size and location, and stretch factor.
        /// This should be refactored when we switch to full viewport zooming.
        /// </summary>
        public void Manipulate(Size _containerSize, double _stretchFactor, bool _fillContainer)
        {
            // Note: the reference size already takes image rotation into account.
            // rotatedCanvas is a different thing and was meant for Kinogram but is not used right now.
            Size referenceSize = reader.Geometry.ReferenceSize;
            stretchFactor = _stretchFactor;
            Size stretchedSize = new Size((int)(referenceSize.Width * stretchFactor), (int)(referenceSize.Height * stretchFactor));

            //if (rotatedCanvas)
            //{
            //    referenceSize = new Size(referenceSize.Height, referenceSize.Width);
            //    stretchedSize = new Size(stretchedSize.Height, stretchedSize.Width);
            //}

            if (!stretchedSize.FitsIn(_containerSize) || _fillContainer)
            {
                // Ratio stretch based on the reference size.
                renderingSize = FitHelper.Fit(stretchedSize, _containerSize, true);
                stretchFactor = (double)renderingSize.Width / referenceSize.Width;
            }
            else
            {
                renderingSize = stretchedSize;
            }

            log.DebugFormat("Presentation size: {0}x{1}.", renderingSize.Width, renderingSize.Height);

            // Center the window in the container.
            renderingLocation = new Point(
                (_containerSize.Width - renderingSize.Width) / 2, 
                (_containerSize.Height - renderingSize.Height) / 2);
        }


        /// <summary>
        /// Compute the presentation window size and location.
        /// Ensures the result has the exact aspect ratio as the reference size
        /// and is even in both dimensions.
        /// </summary>
        public void Manipulate2(Size containerSize, double stretch, bool fitToViewport)
        {
            // When we switch to full viewport zooming this should take the zoom factor instead.
            stretchFactor = stretch;

            Size referenceSize = reader.Geometry.ReferenceSize;
            bool allowBeyondViewport = false;

            // Reference multiplier is how we go from simplified ratio (ex: 16x9) to our current size.
            // ratioWidth and ratioHeight receive the simplified ratio.
            //
            // All the computations are done by multiplying the base ratio, to make sure the result
            // doesn't introduce any mismatch in the coordinate system.
            // Furthermore, we need the output size to be even, so the decoder/scaler can work with them.
            FitHelper.GetReferenceMultiplier(referenceSize, out int ratioWidth, out int ratioHeight, out int referenceMultiplier);

            // Get lower bound multiplier that produces a size >= 32 in both dimensions.
            int minMultiplier = FitHelper.GetMinimumMultiplier(ratioWidth, ratioHeight, 32);

            // Get the maximum multiplier that fits in the viewport.
            int viewportMultiplier = FitHelper.GetFitEvenMultiplier(containerSize, ratioWidth, ratioHeight);

            int presentationMultiplier;
            if (fitToViewport)
            {
                presentationMultiplier = viewportMultiplier;
            }
            else
            {
                double desiredMultiplier = referenceMultiplier * stretch;
                presentationMultiplier = FitHelper.GetNearestEvenMultiplier(desiredMultiplier);

                if (!allowBeyondViewport)
                {
                    presentationMultiplier = Math.Min(presentationMultiplier, viewportMultiplier);
                }
            }

            presentationMultiplier = Math.Max(presentationMultiplier, minMultiplier);

            renderingSize = new Size(ratioWidth * presentationMultiplier, ratioHeight * presentationMultiplier);
            
            stretchFactor = (double)renderingSize.Width / referenceSize.Width;

            // Center the window in the container.
            renderingLocation = new Point(
                (containerSize.Width - renderingSize.Width) / 2,
                (containerSize.Height - renderingSize.Height) / 2);

            log.DebugFormat("Viewport size: {0}x{1}.", containerSize.Width, containerSize.Height);
            log.DebugFormat("Base ratio size: {0}x{1}.", ratioWidth, ratioHeight);
            log.DebugFormat("Viewport size fit: {0}x{1} ({2}x).", ratioWidth * viewportMultiplier, ratioHeight * viewportMultiplier, viewportMultiplier);
            log.DebugFormat("Presentation size: {0}x{1} ({2}x).", renderingSize.Width, renderingSize.Height, presentationMultiplier);
        }
    }
}
