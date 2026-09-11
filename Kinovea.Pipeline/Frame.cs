using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kinovea.Pipeline
{
    /// <summary>
    /// Frame type used in capture.
    /// Simple byte buffer. Format agnostic.
    /// The whole buffer might not be filled with payload.
    /// The camera managers produce the payload, not directly frames.
    /// Frames are always built by stuffing the camera-produced payload into an 
    /// already allocated Frame object.
    /// For the player frames we have a different type in Kinovea.Video.VideoFrame.
    /// </summary>
    public class Frame
    {
        public byte[] Buffer { get; private set; }
        
        public int PayloadLength { get; set; }

        /// <summary>
        /// Monotonically increasing and sequential frame id. 
        /// This id is set by the consumer, when pushing the frame
        /// to the delay buffer.
        /// </summary>
        public long FrameId { get; set; }

        /// <summary>
        /// Create a new frame and allocate the buffer.
        /// </summary>
        public Frame(int bufferSize)
        {
            this.Buffer = new byte[bufferSize];
        }

        /// <summary>
        /// Copy the source frame into this frame.
        /// Assumes pre-allocation and compatible sizes.
        /// </summary>
        public void Import(Frame source)
        {
            System.Buffer.BlockCopy(source.Buffer, 0, this.Buffer, 0, source.PayloadLength);
            this.PayloadLength = source.PayloadLength;
            this.FrameId = source.FrameId;
        }
    }
}
