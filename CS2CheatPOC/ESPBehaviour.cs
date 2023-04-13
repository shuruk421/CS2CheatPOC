
using CS2CheatPOC.Classes;
using Newtonsoft.Json;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace CS2CheatPOC
{
    public class ESPBehaviour : WebSocketBehavior
    {
        protected override void OnMessage(MessageEventArgs e)
        {
            if (e.Data == "GET")
            {
                var lines = CheatClass.GetLines();
                Send(JsonConvert.SerializeObject(lines));
            }
        }
    }
}
