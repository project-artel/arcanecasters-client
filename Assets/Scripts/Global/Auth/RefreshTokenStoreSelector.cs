using Data;

namespace Global.Auth
{
    /// <summary>
    /// 지금 실행 중인 플랫폼에 맞는 <see cref="IRefreshTokenStore"/> 를 만든다. 각 구현은 그
    /// 플랫폼에서만 compile 되도록 파일 전체가 guard 안에 있다. Windows 의 crypt32 P/Invoke 나
    /// macOS 의 Security framework P/Invoke 는 다른 플랫폼 빌드에 들어가면 실행 중에
    /// DllNotFoundException 으로 터진다.
    /// </summary>
    public static class RefreshTokenStoreSelector
    {
        /// <summary>
        /// Editor 를 먼저 가른다. Editor 는 build target 의 define 도 함께 켜므로, build target 이
        /// Android 나 WebGL 이어도 UNITY_ANDROID / UNITY_WEBGL 이 잡힌다. 그대로 두면 play mode
        /// 에서 AndroidJavaClass 나 jslib 을 부르다 실패한다. 그래서 Editor 에서는 build target 이
        /// 아니라 Editor 가 도는 운영체제에 맞춰 desktop 구현을 고른다.
        /// </summary>
        public static IRefreshTokenStore SelectForCurrentPlatform()
        {
            if (MatchingServerCatalog.LocalQaEnabled) return new InMemoryRefreshTokenStore();
#if UNITY_EDITOR_WIN
            return new WindowsDpapiRefreshTokenStore();
#elif UNITY_EDITOR_OSX
            return new MacKeychainRefreshTokenStore();
#elif UNITY_EDITOR_LINUX
            // UNITY_EDITOR_LINUX 가 bare UNITY_EDITOR 보다 먼저 와야 한다. 순서가 바뀌면
            // 아래 UNITY_EDITOR 분기가 Linux Editor 를 먼저 삼킨다.
            return new LinuxFileRefreshTokenStore();
#elif UNITY_EDITOR
            // UNITY_EDITOR_WIN / UNITY_EDITOR_OSX / UNITY_EDITOR_LINUX 가 모든 Editor 운영체제를
            // 가르므로 이 분기는 원래 오지 않는다. 그래도 새 Editor 플랫폼이 생겨 여기로 떨어지면
            // session 안에서만 유지되는 InMemoryRefreshTokenStore 로 안전하게 빠진다.
            return new InMemoryRefreshTokenStore();
#elif UNITY_WEBGL
            // 브라우저마다가 아니라 page 마다 갈린다. page 의 domain 이 account server 와 다르면
            // cookie 가 저장되지 않으므로 그 page 에서는 메모리 저장소로 떨어진다.
            return BrowserCookieRefreshTokenStore.SelectForCurrentPage(ServerList.AccountServer.host);
#elif UNITY_ANDROID
            return new AndroidKeyStoreRefreshTokenStore();
#elif UNITY_STANDALONE_WIN
            return new WindowsDpapiRefreshTokenStore();
#elif UNITY_STANDALONE_OSX
            return new MacKeychainRefreshTokenStore();
#elif UNITY_STANDALONE_LINUX
            return new LinuxFileRefreshTokenStore();
#else
            return new InMemoryRefreshTokenStore();
#endif
        }
    }
}
