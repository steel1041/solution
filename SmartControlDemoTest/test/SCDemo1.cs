using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace smartContractDemo
{
    public class SCDemo1:ITest
    {
        string api = "http://192.168.0.101:59908/api/privatenet";

        public string Name => "智能合约3连 1/3";

        public string ID => "SC1/3";

        public async Task Demo()
        {
            string scriptaddress = "0xc5d3b34befad74a8098bb740523c420e3a2e3a11";
            string key = "68656c6c6f776f726c64";
            var rev = ThinNeo.Helper.HexString2Bytes(key).Reverse().ToArray();
            var revkey = ThinNeo.Helper.Bytes2HexString(rev);

            var url = Helper.MakeRpcUrl(api, "getstorage", new MyJson.JsonNode_ValueString(scriptaddress), new MyJson.JsonNode_ValueString(key));

            string result = await Helper.HttpGet(url);
            Console.WriteLine("得到的结果是：" + result);
        }
    }
}
