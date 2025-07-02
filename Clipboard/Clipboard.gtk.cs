namespace Microsoft.Maui.ApplicationModel.DataTransfer
{
    partial class ClipboardImplementation : IClipboard
    {        
        public Task SetTextAsync(string text)
        {            
            return Task.CompletedTask;
        }

        public bool HasText 
            => throw new NotImplementedException();

        public Task<string> GetTextAsync()
            => throw new NotImplementedException();

        //string GetPasteboardText()
        //=> Pasteboard.ReadObjectsForClasses(
        //        new ObjCRuntime.Class[] { new ObjCRuntime.Class(typeof(NSString)) },
        //        null)?[0]?.ToString();

        void StartClipboardListeners()
            => throw ExceptionUtils.NotSupportedOrImplementedException;

        void StopClipboardListeners()
            => throw ExceptionUtils.NotSupportedOrImplementedException;
    }
}
