using Kinovea.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kinovea.Video
{
    /// <summary>
    /// Representation of a seek operation progress.
    /// Used for feedback in the UI.
    /// </summary>
    public class SeekProgress
    {
        /// <summary>
        /// Version number increased each time the seek changes location.
        /// </summary>
        public long Version { get; private set; }

        /// <summary>
        /// Whether a seek operation is in progress.
        /// </summary>
        public bool InProgress
        {
            get { return !Section.IsEmpty; }
        }

        /// <summary>
        /// The section of the video that has been covered 
        /// by the seek operation so far.
        /// Starts at the keyframe where the seek landed and
        /// ends at the last frame decoded, until it reaches
        /// the actual target of the seek.
        /// </summary>
        public VideoSection Section { get; private set; }

        public SeekProgress(long version, VideoSection section)
        {
            Version = version;
            Section = section;
        }

        public static SeekProgress MakeEmpty()
        {
            return new SeekProgress(-1, VideoSection.MakeEmpty());
        }
    }
}
