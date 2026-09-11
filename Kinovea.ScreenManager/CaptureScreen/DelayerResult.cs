using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kinovea.ScreenManager
{
    /// <summary>
    /// Result of a frame request.
    /// </summary>
    public enum DelayerResult
    {
        Success,
        TooSoon,
        TooLate,
        NotAllocated
    }
}
