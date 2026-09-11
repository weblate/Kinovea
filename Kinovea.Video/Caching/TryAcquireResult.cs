using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kinovea.Video
{
    public class TryAcquireResult
    {
        /// <summary>
        /// Whether the target was found in the cache.
        /// If true `Current` is now pointing to it.
        /// </summary>
        public bool TargetAcquired { get; }

        /// <summary>
        /// The actual timestamp of the matching frame if found.
        /// </summary>
        public long AcquiredTimestamp { get; }

        /// <summary>
        /// Whether the cache is contiguous and can be used for 
        /// timestamp based requests.
        /// </summary>
        public bool IsContiguous { get; }

        public TryAcquireResult(
            bool targetAcquired, 
            long acquiredTimestamp,
            bool isContiguous)
        {
            TargetAcquired = targetAcquired;
            AcquiredTimestamp = acquiredTimestamp;
            IsContiguous = isContiguous;
        }

        public static TryAcquireResult MakeEmpty()
        {
            return new TryAcquireResult(false, -1, false);
        }
    }
}
