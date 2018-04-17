using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace smartContractDemo
{
    public class Nep55 : ITest
    {
        //public const string api = "https://api.nel.group/api/testnet";

        public const string api = "http://192.168.0.101:59908/api/privatenet";

        public const string id_GAS = "0x602c79718b16e442de58778e148d0b1084e3b2dffd5de6b7b16cee7969282de7";
        //wallet1
        public const string testwif = "KzprnMDQHhK7jnJ3dNNq5C2AfJdy58oGyphnZtc6t78NE26nhq7S";

        public string Name => "Nep5.5 查我的GAS";

        public string ID => "N5";
        async public Task Demo()
        {
            byte[] prikey = ThinNeo.Helper.GetPrivateKeyFromWIF(testwif);
            byte[] pubkey = ThinNeo.Helper.GetPublicKeyFromPrivateKey(prikey);
            string address = ThinNeo.Helper.GetAddressFromPublicKey(pubkey);
            byte[] scripthash = ThinNeo.Helper.GetPublicKeyHashFromAddress(address);
            Console.WriteLine("address=" + address);

     

            var url = Helper.MakeRpcUrl(api, "getbalance", new MyJson.JsonNode_ValueString(address));
            string result = await Helper.HttpGet(url);

            Console.WriteLine("得到的结果是：" + result);
            var json = MyJson.Parse(result).AsDict()["result"].AsList();
            foreach(var item in json)
            {
                if(item.AsDict()["asset"].AsString()== id_GAS)
                {
                    Console.WriteLine("gas=" + item.AsDict()["balance"].ToString());
                }
            }
        }
    }
}
