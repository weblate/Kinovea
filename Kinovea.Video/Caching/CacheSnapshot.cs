using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kinovea.Services;

namespace Kinovea.Video
{
    /// <summary>
    /// Representation of the cache state at a given moment.
    /// Only used for feedback in the UI.
    /// </summary>
    public class CacheSnapshot
    {
        /// <summary>
        /// Version number increased each time the cache is modified.
        /// </summary>
        public long Version { get; private set; }

        /// <summary>
        /// Sections of the video that are currently cached.
        /// All frames within the section must be present.
        /// </summary>
        public VideoSection[] Sections { get; private set; }

        public CacheSnapshot(long version, VideoSection[] spans)
        {
            Version = version;
            Sections = spans;
        }

        public static CacheSnapshot MakeEmpty()
        {
            return new CacheSnapshot(-1, new VideoSection[0]);
        }
    }
}
