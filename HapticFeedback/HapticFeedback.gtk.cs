using Microsoft.Maui.ApplicationModel;
using System.Runtime.InteropServices.JavaScript;

namespace Microsoft.Maui.Devices
{
    class HapticFeedbackImplementation : IHapticFeedback
    {
        public bool IsSupported => false;

        public void Perform(HapticFeedbackType type)
            => throw ExceptionUtils.NotSupportedOrImplementedException;
    }
}
