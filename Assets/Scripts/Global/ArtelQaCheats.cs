#if ARTEL_GAME_FUNCTIONS && (UNITY_EDITOR || DEVELOPMENT_BUILD)
using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Text;
using Artel;
using Data;
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

        private static IEnumerator Post(string url, object payload, Action<JObject> receive = null)
        {
            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload)));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 10;
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + SceneContext.JwtToken);
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
                throw new InvalidOperationException($"QA 요청 실패 (HTTP {request.responseCode})");
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
