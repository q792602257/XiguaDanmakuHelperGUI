using Newtonsoft.Json.Linq;

namespace XiguaDanmakuHelper
{
    public struct User
    {
        private long ID;
        private readonly string Name;

        public User(JObject j)
        {
            ID = 0;
            Name = "";
            if (j["data"]?["anchorInfo"] != null)
            {
                ID = (long) j["data"]["anchorInfo"]["id"];
                Name = (string) j["data"]["anchorInfo"]["name"];
            }

            if (j["Msg"]?["user"] != null)
            {
                ID = (long) j["Msg"]["user"]["user_id"];
                Name = (string) j["Msg"]["user"]["name"];
            }

        }

        public override string ToString()
        {
            return $"{Name}";
        }
    }
}