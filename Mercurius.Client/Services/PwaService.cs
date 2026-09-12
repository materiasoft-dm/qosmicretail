using Microsoft.JSInterop;

namespace Mercurius.Client.Services
{
    // See wwwroot/js/pwaHelpers.js for what's actually happening — persistent storage requests
    // and the install-prompt capture both have to live in JS (no .NET equivalent).
    public class PwaService
    {
        private readonly IJSRuntime _js;
        public PwaService(IJSRuntime js) => _js = js;

        public async Task<bool> RequestPersistentStorageAsync()
        {
            try
            {
                return await _js.InvokeAsync<bool>("mercuriusPwa.requestPersistentStorage");
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> IsInstalledAsync() => await _js.InvokeAsync<bool>("mercuriusPwa.isInstalled");

        public async Task<bool> CanPromptInstallAsync() => await _js.InvokeAsync<bool>("mercuriusPwa.canPromptInstall");

        /// <returns>"accepted", "dismissed", or "unavailable".</returns>
        public async Task<string> PromptInstallAsync() => await _js.InvokeAsync<string>("mercuriusPwa.promptInstall");
    }
}
