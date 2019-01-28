using Newtonsoft.Json.Linq;

namespace XiguaDanmakuHelper
{
    public struct Chat
    {
        public string content;
        public User user;

        public Chat(JObject j)
        {
            content = "";
            user = new User(j);
            if (j["Msg"]?["content"] != null) content = (string) j["Msg"]["content"];
        }

        public override string ToString()
        {
            return $"{user} : {content}";
        }
    }
}