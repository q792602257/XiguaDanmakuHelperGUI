using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace XiguaDanmakuHelper
{
    public struct Gift
    {
        public User user;
        private readonly long ID;
        public long count;
        public static Dictionary<long, string> GiftList = new Dictionary<long, string>();

        public Gift(JObject j)
        {
            ID = 0;
            count = 0;
            user = new User(j);
            if (j["Msg"]?["present_end_info"] != null)
            {
                ID = (long) j["Msg"]["present_end_info"]["id"];
                count = (long) j["Msg"]["present_end_info"]["count"];
            }

            if (j["Msg"]?["present_info"] != null)
            {
                ID = (long) j["Msg"]["present_info"]["id"];
                count = (long) j["Msg"]["present_info"]["repeat_count"];
            }
        }

        public static void UpdateGiftList(long roomId)
        {
            GiftList = new Dictionary<long, string>();
            GiftList.Add(10001, "西瓜");
            var _text = Common.HttpGet($"https://live.ixigua.com/api/gifts/{roomId}");
            var j = JObject.Parse(_text);
            if (j["data"] != null)
                foreach (var g in j["data"])
                    GiftList.Add((long) g["ID"], (string) g["Name"]);
        }

        public override string ToString()
        {
            return $"感谢 {user} 送出的 {count} 个 {GetName()}";
        }

        public string GetName()
        {
            string GiftN;
            if (GiftList.ContainsKey(ID))
                GiftN = GiftList[ID];
            else
                GiftN = $"未知礼物{ID}";

            return GiftN;
        }
    }
}