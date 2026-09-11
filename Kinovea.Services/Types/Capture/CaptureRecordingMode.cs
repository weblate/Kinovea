using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kinovea.Services
{
    public enum CaptureRecordingMode
    {
        /// <summary>
        /// In this mode the camera feed goes through the delay buffer before being pulled for recording.
        /// Encoding is done on the fly in a separate thread.
        /// Encoding is allowed to fall behind camera frame rate as long as the frames it needs are still in the buffer.
        /// If encoding can sustain camera frame rate this can be used to record videos of unlimited length.
        /// If encoding can't sustain camera frame rate the delay buffer should be set to cover the 
        /// expected recording length. Otherwise we'll get drops at the end.
        /// </summary>
        Delay, 

        /// <summary>
        /// In this mode the camera feed goes through the delay buffer, but recording isn't done on the fly.
        /// At the recording stop event, the feed is frozen, and frames are taken from the delay buffer 
        /// and sent to storage all at once. This alleviates encoding perfs issues but only allow 
        /// for time-limited recording, based on the delay buffer capacity.
        /// </summary>
        Scheduled
    }
}
