using System.Collections.Generic;
using Global;
using UnityEngine;

namespace Data
{
    /// <summary>
    /// Holds every matching server an admin may switch to and the one currently in use.
    /// <see cref="ServerList.MatchingServer"/> reads <see cref="Current"/>, so switching here
    /// redirects every later matching server request.
    ///
    /// Non-admin sessions never call <see cref="Select"/> and therefore always stay on
    /// <see cref="BuildDefault"/>.
    /// </summary>
    public static class MatchingServerCatalog
    {
        private const string SelectedKeyPreference = "MatchingServer.SelectedKey";

        public static readonly MatchingServerOption Local =
            new MatchingServerOption("local", "127.0.0.1", new Server("local", "127.0.0.1", 6209));

        /// <summary>
        /// The magic-card track's own environment. It runs its own account server with its own
        /// key pair, so <see cref="ServerList.AccountServer"/> has to point at the same
        /// environment: this lobby verifies tokens against that account server's JWKS and
        /// rejects one signed by any other.
        /// </summary>
        public static readonly MatchingServerOption MagicCard =
            new MatchingServerOption("magic-card", "magic-card",
                new Server("magic-card", "lobby.magic-card.ac.theevilent.com", 443, true));

        public static readonly MatchingServerOption Dev =
            new MatchingServerOption("dev", "dev", new Server("dev", "lobby.dev.ac.theevilent.com", 443, true));

        public static readonly MatchingServerOption Deploy =
            new MatchingServerOption("deploy", "deploy", new Server("deploy", "lobby.ac.theevilent.com", 443, true));

        /// <summary>
        /// Options in auto-select priority order: the first healthy one wins when the admin
        /// has no stored choice.
        /// </summary>
        public static readonly IReadOnlyList<MatchingServerOption> Options = new[] { Local, MagicCard, Dev, Deploy };

        /// <summary>
        /// 관리자 선택 전에는 운영 서버를 사용한다. 개발 빌드도 같은 기본값을 쓰며,
        /// 관리자는 드롭다운에서 다른 서버를 선택할 수 있다.
        /// </summary>
        public static bool LocalQaEnabled { get; } =
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            System.Environment.GetEnvironmentVariable("ARCANECASTERS_LOCAL_QA") == "1";
#else
            false;
#endif

        private static readonly MatchingServerOption BuildDefault = LocalQaEnabled ? Local : Deploy;

        public static MatchingServerOption Current { get; private set; } = BuildDefault;

        public static void Select(MatchingServerOption option)
        {
            if (LocalQaEnabled || option == null || option == Current)
                return;

            Current = option;
            PlayerPrefs.SetString(SelectedKeyPreference, option.Key);
            PlayerPrefs.Save();
            WDebug.Log($"[MatchingServerCatalog] matching server switched to {option.DisplayName} ({option.Server.url})");
        }

        /// <summary>
        /// Returns the option the admin last picked on this device, or null when there is none.
        /// </summary>
        public static MatchingServerOption FindStoredOption()
        {
            if (LocalQaEnabled) return Local;
            string storedKey = PlayerPrefs.GetString(SelectedKeyPreference, null);
            if (string.IsNullOrEmpty(storedKey))
                return null;

            foreach (MatchingServerOption option in Options)
            {
                if (option.Key == storedKey)
                    return option;
            }

            return null;
        }
    }
}
