using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Den.Dev.Conch.Authentication;
using Den.Dev.Conch.Storage;
using Den.Dev.FrameDrop.Media;
using Den.Dev.FrameDrop.Models;

namespace Den.Dev.FrameDrop.Desktop.Services
{
    public class AuthService
    {
        private readonly EncryptedFileTokenStore tokenStore;

        public AuthService()
        {
            this.tokenStore = new EncryptedFileTokenStore(FrameDropConfiguration.DefaultTokenCachePath);
        }

        public bool HasValidTokens()
        {
            var cache = this.tokenStore.Load();
            return cache != null && !string.IsNullOrEmpty(cache.XstsToken);
        }

        public (string? AuthHeader, string? XUID, string? Gamertag) GetCachedAuthInfo()
        {
            var cache = this.tokenStore.Load();
            if (cache == null)
            {
                return (null, null, null);
            }

            return (cache.AuthorizationHeaderValue, cache.XUID, cache.Gamertag);
        }

        public SISUSessionManager CreateSessionManager(HttpClient? httpClient = null)
        {
            return new SISUSessionManager(this.tokenStore, FrameDropConfiguration.AppConfiguration, httpClient);
        }

        public async Task<bool> TryRestoreSessionAsync(CancellationToken cancellationToken)
        {
            var sessionManager = this.CreateSessionManager();
            var restored = await sessionManager.TryRestoreSessionAsync(cancellationToken);
            return restored != null;
        }

        public void Logout()
        {
            this.tokenStore.Clear();
        }

        public IXboxMediaClient CreateMediaClient()
        {
            var (authHeader, xuid, _) = this.GetCachedAuthInfo();
            if (string.IsNullOrEmpty(authHeader) || string.IsNullOrEmpty(xuid))
            {
                throw new InvalidOperationException("Not authenticated.");
            }

            var sessionManager = this.CreateSessionManager();
            return new XboxMediaClient(authHeader, xuid, sessionManager, this.tokenStore);
        }
    }
}
