using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace XiguaDanmakuHelper
{
    public class Api
    {
        public delegate void Log(string msg);

        public delegate void RoomCounting(long viewer, long popularity);

        public delegate void WhenAd();

        public delegate void WhenChat(Chat chat);

        public delegate void WhenGift(Gift gift);

        public delegate void WhenGifting(Gift gift);

        public delegate void WhenJoin(User user);

        public delegate void WhenLeave();

        public delegate void WhenLiked(User user);

        public delegate void WhenMessage(MessageModel m);

        public delegate void WhenSubscribe(User user);

        private int _rand;
        private JObject _rawRoomInfo;
        private long _roomPopularity;
        private long _roomViewer;
        protected string cursor = "";
        public bool isLive;
        public bool isValidRoom;
        private long RoomID;
        public string Title = "";
        public User user;
        private readonly long UserID;

        public Api()
        {
            UserID = 97621754276;
        }

        public Api(long userId)
        {
            UserID = userId;
        }

        public event WhenChat OnChat;
        public event WhenGifting OnGifting;
        public event WhenGift OnGift;
        public event WhenJoin OnJoin;
        public event WhenSubscribe OnSubscribe;
        public event WhenLiked OnLiked;
        public event WhenAd OnAd;
        public event WhenLeave OnLeave;
        public static event WhenMessage OnMessage;
        public static event RoomCounting OnRoomCounting;
        public static event Log LogMessage;

        public async Task<bool> ConnectAsync()
        {
            await UpdateRoomInfo();
            if (!isValidRoom)
            {
                LogMessage?.Invoke("请确认输入的用户ID是否正确");
                return false;
            }

            if (!isLive)
            {
                LogMessage?.Invoke("主播未开播");
                return false;
            }

            Gift.UpdateGiftList(RoomID);
            EnterRoom();
//            LogMessage?.Invoke("连接成功");
            return true;
        }

        public void _updateRoomInfo(JObject j)
        {
            if (j["Msg"]?["member_count"] != null) _roomViewer = (long) j["Msg"]["member_count"];
            if (j["Msg"]?["popularity"] != null) _roomPopularity = (long) j["Msg"]["popularity"];

            OnRoomCounting?.Invoke(_roomViewer, _roomPopularity);
        }

        public async Task<bool> UpdateRoomInfo()
        {
            var url = $"https://live.ixigua.com/api/room?anchorId={UserID}";
            var _text = await Common.HttpGetAsync(url);
            var j = JObject.Parse(_text);
            if (j["data"]?["title"] is null || j["data"]?["id"] is null)
            {
                LogMessage?.Invoke("无法获取RoomID，请与我联系");
                Console.Read();
                return false;
            }

            isValidRoom = true;
            _rawRoomInfo = (JObject) j["data"];
            Title = (string) j["data"]["title"];
            RoomID = (long) j["data"]["id"];
            user = new User(j);

            isLive = (int) j["data"]?["status"] == 2;
            return true;
        }

        public async void EnterRoom()
        {
            if (!isValidRoom) return;
            await Common.HttpPostAsync($"https://live.ixigua.com/api/room/enter/{RoomID}");
        }

        public void GetDanmaku()
        {
            if (!isValidRoom) return;

            var _text =
                Common.HttpGet($"https://live.ixigua.com/api/msg/list/{RoomID}?AnchorID={UserID}&Cursor={cursor}");
            var j = JObject.Parse(_text);
            if (j["data"]?["Extra"]?["Cursor"] is null)
            {
                LogMessage?.Invoke("数据结构改变，请与我联系");
                Console.Read();
                return;
            }

            cursor = (string) j["data"]["Extra"]["Cursor"];
            if (j["data"]?["LiveMsgs"] is null)
            {
                UpdateRoomInfo();
                return;
            }

            foreach (var m in j["data"]["LiveMsgs"])
            {
                if (m?["Method"] is null) continue;
                switch ((string) m["Method"])
                {
                    case "VideoLivePresentMessage":
                        OnGifting?.Invoke(new Gift((JObject) m));
                        OnMessage?.Invoke(new MessageModel(MessageEnum.Gifting, new Gift((JObject) m)));
                        break;
                    case "VideoLivePresentEndTipMessage":
                        OnGift?.Invoke(new Gift((JObject) m));
                        OnMessage?.Invoke(new MessageModel(MessageEnum.Gift, new Gift((JObject) m)));
                        break;
                    case "VideoLiveRoomAdMessage":
                        OnAd?.Invoke();
                        OnMessage?.Invoke(new MessageModel(MessageEnum.Ad, (JObject) m));
                        break;
                    case "VideoLiveChatMessage":
                        OnChat?.Invoke(new Chat((JObject) m));
                        OnMessage?.Invoke(new MessageModel(new Chat((JObject) m)));
                        break;
                    case "VideoLiveMemberMessage":
                        _updateRoomInfo((JObject) m);
//                        OnEnter?.Invoke(new Gift((JObject)m));
//                        OnMessage?.Invoke(new MessageModel(new Chat((JObject)m)));
                        OnMessage?.Invoke(new MessageModel(MessageEnum.Enter, (JObject) m));
                        break;
                    case "VideoLiveSocialMessage":
                        OnSubscribe?.Invoke(new User((JObject) m));
                        OnMessage?.Invoke(new MessageModel(MessageEnum.Subscribe, new User((JObject) m)));
                        break;
                    case "VideoLiveJoinDiscipulusMessage":
                        OnJoin?.Invoke(new User((JObject) m));
                        OnMessage?.Invoke(new MessageModel(MessageEnum.Join, new User((JObject) m)));
                        break;
                    case "VideoLiveControlMessage":
                        OnLeave?.Invoke();
                        OnMessage?.Invoke(new MessageModel(MessageEnum.Leave));
                        break;
                    case "VideoLiveDiggMessage":
                        OnLiked?.Invoke(new User((JObject) m));
                        OnMessage?.Invoke(new MessageModel(MessageEnum.Like, new User((JObject) m)));
                        break;
                    default:
                        OnMessage?.Invoke(new MessageModel(MessageEnum.Other, (JObject) m));
                        break;
                }
            }
        }

        public string GetTitle()
        {
            if (!isValidRoom) return "无法获取直播间信息";
            if (_rand <= 5) return user.ToString();
            if (_rand >= 10) _rand = 0;
            return $"观众:{_roomViewer} 人气:{_roomPopularity}";
        }
    }
}