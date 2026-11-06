namespace Data
{
    public static class ServerList
    {
        /// <summary>
        /// Matching/lobby server currently in use. Admins can switch it at runtime from the lobby
        /// dropdown; see <see cref="MatchingServerCatalog"/> for the candidates and the default.
        /// </summary>
        public static Server MatchingServer => MatchingServerCatalog.Current.Server;

        /// <summary>
        /// Must sit in the same environment as <see cref="MatchingServer"/>. This server signs
        /// the session token and publishes the JWKS that both the lobby and
        /// <c>JwksService</c> verify it with, so a token from another environment is rejected.
        ///
        /// Host is <c>account.theevilent.com</c>, not <c>account.ac.yunseong.dev</c>: the WebGL
        /// build serves from <c>arcanecasters.theevilent.com</c>, and a refresh cookie is only
        /// sent back to a request whose host shares its registrable domain. Same server
        /// (168.107.6.191), and the certificate already covers this name.
        /// </summary>
        public static readonly Server AccountServer = MatchingServerCatalog.LocalQaEnabled
            ? new Server("local", "127.0.0.1", 6210)
            : new Server("deploy", "account.theevilent.com", 443, true);
    }
}
