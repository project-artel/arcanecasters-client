#if ARTEL_GAME_FUNCTIONS && (UNITY_EDITOR || DEVELOPMENT_BUILD)
using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Text;
using Artel;
using Data;
using Data.Deck;
using DeckScene;
using Global.Auth;
using Global.Serialization;
using LobbyScene;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace Global
{
    public static class ArtelQaCheats
    {
        public enum Side { Self, Opponent }
        public enum Unit
        {
            FireSlime, WaterSlime, RockSlime, ElectricSlime, LeafSlime, WindSlime,
            RockGolem, AquaArcher, GroundTower
        }

        [ArtelCheat("게임 계정을 로그아웃하고 저장된 세션을 지운 뒤 로그인 화면으로 돌아갑니다. ARTEL 연결은 유지합니다. 게스트 계정은 다시 로그인할 수 없습니다.",
            when: nameof(HasAuthSession))]
        public static IEnumerator LogoutToLogin()
        {
            var session = AuthSession.Instance;
            yield return session.StartCoroutine(session.Logout());
            yield return SceneManager.LoadSceneAsync("LoginScene");
            yield return "scene=LoginScene; loggedOut=true";
        }

        [ArtelCheat("QA 봇 매치를 만들고 전투에 입장합니다.", when: nameof(InLobby))]
        public static IEnumerator StartQaBotMatch() => EnterMatch(null);

        [ArtelCheat("지정한 QA 계정과 듀얼을 시작합니다. 상대도 내 계정 ID로 호출하면 같은 매치에 합류합니다.",
            when: nameof(InLobby))]
        public static IEnumerator StartQaDuel(string peerId) => EnterMatch(PositiveId(peerId));

        [ArtelCheat("로그인한 QA 계정에 지정한 마법을 지급합니다. 다음 매치에서 보유 목록에 반영됩니다.",
            when: nameof(InLobby))]
        public static IEnumerator GrantMagic(string magicId, [CheatRange(1, 100)] int count)
        {
            var id = PositiveId(magicId);
            yield return Post(LobbyUrl + "/magics", new { magicId = id, count });
        }

        [ArtelCheat("로그인한 QA 계정의 모든 플레이어용 마법을 각각 최소 minimumCount장 보유하도록 부족분만 지급합니다. 15장이면 같은 마법만으로 덱을 만들 수 있습니다. 중단 후 다시 호출하면 부족분부터 보충합니다.",
            when: nameof(InLobby))]
        public static IEnumerator GrantAllMagics([CheatRange(1, 100)] int minimumCount)
        {
            if (minimumCount < 1 || minimumCount > 100)
                throw new ArgumentOutOfRangeException(nameof(minimumCount));

            var userId = SceneContext.UserID;
            var server = ServerList.MatchingServer.url.TrimEnd('/');
            var deadline = UnityEngine.Time.realtimeSinceStartup + 40f;
            CardDto[] cards = null;
            yield return ReadCardPool(server, value => cards = value);
            var pending = cards.Where(card => card.count < minimumCount).OrderBy(card => card.id).ToArray();
            DeckManagementViewModel.ClearCachedUserData();

            var completed = 0;
            foreach (var card in pending)
            {
                var context = $"전체 마법 지급 {completed}/{pending.Length}종 완료, magicId={card.id}. ";
                if (!InLobby() || SceneContext.UserID != userId || ServerList.MatchingServer.url.TrimEnd('/') != server)
                    throw new InvalidOperationException(context + "계정 또는 서버·화면이 바뀌어 중단했습니다.");
                // SDK가 치트를 60초 후 중단하므로 마지막 요청과 보유 목록 재조회 시간을 남긴다.
                if (UnityEngine.Time.realtimeSinceStartup >= deadline)
                    throw new InvalidOperationException(context + "시간 제한으로 중단했습니다. 다시 호출하면 부족분만 보충합니다.");

                var amount = minimumCount - card.count;
                yield return Post(server + "/api/artel-qa/magics", new { magicId = card.id, count = amount },
                    failureContext: context, timeoutSeconds: 5);
                completed++;
            }

            if (!InLobby() || SceneContext.UserID != userId || ServerList.MatchingServer.url.TrimEnd('/') != server)
                throw new InvalidOperationException("지급 후 계정 또는 서버·화면이 바뀌어 보유 수량을 확인하지 못했습니다.");
            yield return ReadCardPool(server, value => cards = value);
            if (cards.Any(card => card.count < minimumCount || !card.unlocked))
                throw new InvalidOperationException("일부 마법의 보유 수량 또는 해금 반영을 확인하지 못했습니다. 다시 호출해 부족분을 보충하세요.");
            yield return $"magics={cards.Length}; toppedUp={completed}; minimumCount={minimumCount}";
        }

        [ArtelCheat("현재 QA 매치에서 지정한 진영의 마나를 설정합니다.", when: nameof(InQaMatch))]
        public static IEnumerator SetMana(Side side, [CheatRange(0, 100000)] int amount) =>
            Command(new { operation = "set_mana", side = side.ToString(), amount });

        [ArtelCheat("현재 QA 매치의 상대 오브젝트에 서버 피해 처리를 적용합니다.", when: nameof(InQaMatch))]
        public static IEnumerator DamageTarget([CheatRange(0, int.MaxValue)] int targetId,
            [CheatRange(1, 100000)] int amount) =>
            Command(new { operation = "damage_target", targetId, amount });

        [ArtelCheat("현재 QA 매치에서 살아 있는 오브젝트의 체력을 설정합니다.", when: nameof(InQaMatch))]
        public static IEnumerator SetHealth([CheatRange(0, int.MaxValue)] int targetId,
            [CheatRange(1, int.MaxValue)] int hp) =>
            Command(new { operation = "set_health", targetId, amount = hp });

        [ArtelCheat("현재 QA 매치에서 손패를 보유 마법 ID 목록으로 교체합니다. 쉼표로 구분해 최대 6개를 지정합니다.",
            when: nameof(InQaMatch))]
        public static IEnumerator SetHand(Side side, string magicIds)
        {
            var ids = string.IsNullOrWhiteSpace(magicIds)
                ? Array.Empty<long>()
                : magicIds.Split(',').Select(id => PositiveId(id.Trim())).ToArray();
            if (ids.Length > 6) throw new ArgumentException("손패에는 최대 6장을 지정할 수 있습니다.");
            return Command(new { operation = "set_hand", side = side.ToString(), magicIds = ids });
        }

        [ArtelCheat("현재 QA 매치에서 지정한 진영의 유닛을 필드 좌표에 소환합니다.", when: nameof(InQaMatch))]
        public static IEnumerator SpawnUnit(Unit unit, Side side, [CheatRange(0, 18)] float x,
            [CheatRange(0, 10)] float z) =>
            Command(new { operation = "spawn_unit", unit = unit.ToString(), side = side.ToString(), x, z });

        private static bool HasAuthSession() => AuthSession.Instance != null;

        private static bool InLobby() =>
            SceneContext.User != null && !string.IsNullOrEmpty(SceneContext.JwtToken)
            && LobbySceneViewModel.Instance != null
            && LobbySceneViewModel.Instance.CurrentState.Data == LobbySceneViewModel.LobbyState.Idle;

        private static bool InQaMatch() =>
            SceneManager.GetActiveScene().name == "GameScene" && !string.IsNullOrEmpty(SceneContext.JwtToken)
            && SceneContext.MatchInfo?.sessionId?.StartsWith("artel-qa-", StringComparison.Ordinal) == true;

        private static string LobbyUrl => ServerList.MatchingServer.url.TrimEnd('/') + "/api/artel-qa";

        private static IEnumerator EnterMatch(long? peerId)
        {
            MatchedInfoDto match = null;
            yield return Post(LobbyUrl + "/matches", new { peerId },
                body => match = body.ToObject<MatchedInfoDto>());
            if (match?.sessionId?.StartsWith("artel-qa-", StringComparison.Ordinal) != true
                || !InLobby())
                throw new InvalidOperationException("QA 매치에 입장할 수 없습니다.");
            LobbySceneViewModel.Instance.EnterFriendMatch(match);
            var deadline = UnityEngine.Time.realtimeSinceStartup + 25f;
            while (!InQaMatch())
            {
                if (UnityEngine.Time.realtimeSinceStartup >= deadline || InLobby())
                    throw new InvalidOperationException("QA 전투 화면을 열지 못했습니다.");
                yield return null;
            }
            yield return $"sessionId={match.sessionId}";
        }

        private static IEnumerator Command(object body)
        {
            var match = SceneContext.MatchInfo;
            var url = match.server.TrimEnd('/') + "/api/artel-qa/matches/" +
                Uri.EscapeDataString(match.sessionId) + "/commands";
            return Post(url, body);
        }

        private static IEnumerator ReadCardPool(string server, Action<CardDto[]> receive)
        {
            using var request = UnityWebRequest.Get(server + "/api/users/mine/cardLists");
            request.timeout = 8;
            Server.SetAcceptLanguage(request);
            Server.SetAuthorization(request);
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
                throw new InvalidOperationException($"보유 마법 목록 조회 실패 (HTTP {request.responseCode})");
            if (!JsonCodec.TryDeserialize(request.downloadHandler.text, out CardPoolDto pool, out _)
                || pool.cards == null || pool.cards.Length == 0
                || pool.cards.Any(card => card == null || card.id <= 0 || card.count < 0)
                || pool.cards.Select(card => card.id).Distinct().Count() != pool.cards.Length)
                throw new InvalidOperationException("보유 마법 목록이 비어 있거나 올바르지 않습니다.");
            receive(pool.cards);
        }

        private static IEnumerator Post(string url, object payload, Action<JObject> receive = null,
            string failureContext = null, int timeoutSeconds = 10)
        {
            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload)));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = timeoutSeconds;
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + SceneContext.JwtToken);
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
                throw new InvalidOperationException($"{failureContext}QA 요청 실패 (HTTP {request.responseCode})");
            var body = JObject.Parse(request.downloadHandler.text);
            receive?.Invoke(body);
            if (receive == null)
                yield return body.Value<string>("message") ?? body.ToString(Formatting.None);
        }

        private static long PositiveId(string value)
        {
            if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
                throw new ArgumentException("양의 정수 ID를 입력하세요.");
            return id;
        }
    }
}
#endif
